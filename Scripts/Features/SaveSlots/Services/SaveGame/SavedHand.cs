using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.SaveSlots.Services.SaveGame;

/// <summary>
///     Writes the player's hand into a slot as one ordered record, every item in hand order with the front item
///     marked, and puts it back. A card is written as a stored card (signature, look seed, price); a container as a
///     whole container record, so a cardboard box keeps its flaps and what it holds. The scene's card and container
///     lists leave out what the hand holds.
/// </summary>
internal static class SavedHand
{
    private const string Holder = "The hand";

    /// <summary>The hand read back from a slot: each item a <see cref="StoredCard" /> or a container record.</summary>
    internal sealed record Saved(int Front, IReadOnlyList<object> Items);

    private sealed class HandRecord
    {
        public int Front { get; set; } = -1;
        public List<ItemRecord> Items { get; set; } = new();
    }

    // Exactly one of the two is set.
    private sealed class ItemRecord
    {
        public SavedStoredItems.Record? Card { get; set; }
        public object? Container { get; set; }
    }

    /// <summary>The player's hand in <paramref name="tree" />, or null when the scene has none.</summary>
    // ASSUMPTION: one player has one hand (Risk Low), so the first hand in the group is the player's.
    internal static IHand? HandIn(SceneTree tree) =>
        tree.GetNodesInGroup(IHand.GroupName).OfType<IHand>().FirstOrDefault();

    /// <summary>The record to write for <paramref name="hand" />.</summary>
    internal static object Capture(IHand hand) => new HandRecord
    {
        Front = hand.FrontIndex,
        Items = hand.Items.Select(ItemOf).OfType<ItemRecord>().ToList()
    };

    private static ItemRecord? ItemOf(Node3D body) => body switch
    {
        CardController card => new ItemRecord { Card = SavedStoredItems.RecordOf(StoredItemBodies.RecordOf(card)) },
        CardContainer container => new ItemRecord { Container = SavedContainers.RecordOf(container) },
        _ => null
    };

    /// <summary>
    ///     Checks the hand record of a slot and returns it. Throws <see cref="FormatException" /> when an item cannot
    ///     be read or the front is not one of the items, so a caller that parses first keeps the scene intact.
    /// </summary>
    internal static Saved Parse(object raw)
    {
        var record = SaveExtension.DeserializeObject<HandRecord>(SaveExtension.SerializeObject(raw))
                     ?? throw new FormatException($"{Holder} record cannot be read");
        var items = record.Items.Select(ParseItem).ToList();
        if (items.Count == 0 ? record.Front != -1 : record.Front < 0 || record.Front >= items.Count)
            throw new FormatException($"{Holder} has front item {record.Front} of {items.Count}");

        return new Saved(record.Front, items);
    }

    private static object ParseItem(ItemRecord item) => item switch
    {
        { Card: { } card, Container: null } => SavedStoredItems.ParseCard(card, Holder),
        { Card: null, Container: { } container } => SavedContainers.Parse(new List<object> { container })[0],
        _ => throw new FormatException($"{Holder} holds an item that is neither one card nor one container")
    };

    /// <summary>
    ///     Empties the player's hand, then builds every saved item under <paramref name="world" /> and gives it to the
    ///     hand in the saved order, and makes the saved front item the front again. An item that cannot be built or
    ///     that the hand refuses is logged; the front then stays on the same saved item when it was built.
    /// </summary>
    internal static void Restore(SceneTree tree, Saved saved, Node3D world, ICardSpawningService? spawning)
    {
        if (HandIn(tree) is not { } hand)
        {
            ILog.Error($"{Holder} was saved but the scene has no hand to restore it to");
            return;
        }

        foreach (var held in hand.Items.ToList())
        {
            // RemoveChild leaves the tree at once, so the hand lets go of it now.
            held.GetParent()?.RemoveChild(held);
            held.QueueFree();
        }

        var front = -1;
        for (var i = 0; i < saved.Items.Count; i++)
        {
            var body = Build(saved.Items[i], world, spawning);
            if (body == null)
            {
                ILog.Error($"{Holder} item {i} could not be built and is lost");
                continue;
            }

            if (!hand.TryTake(body))
            {
                // Built at the world origin (a card) or its saved pose (a container); moved to the player instead.
                if (hand is Node3D at)
                    body.GlobalPosition = at.GlobalPosition;
                ILog.Error($"{Holder} refused saved item {i}; it is left where the player stands");
                continue;
            }

            if (i == saved.Front)
                front = hand.Count - 1;
        }

        if (front >= 0)
            hand.SetFront(front);
    }

    private static Node3D? Build(object item, Node3D world, ICardSpawningService? spawning) => item switch
    {
        StoredCard card => StoredItemBodies.BuildCard(card, world, Transform3D.Identity, spawning),
        SavedContainers.Saved container => SavedContainers.Build(container, world),
        _ => null
    };
}
