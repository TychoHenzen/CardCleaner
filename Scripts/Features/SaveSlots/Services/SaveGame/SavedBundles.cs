using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.SaveSlots.Services.SaveGame;

/// <summary>
///     Writes the decks and piles lying in the scene into a slot and rebuilds them from one: each with its pose, its
///     pile seed and every card it holds, in the order they went in, so a pile reloads as the same heap.
/// </summary>
internal static class SavedBundles
{
    /// <summary>One deck or pile read back from a slot, every field already checked.</summary>
    internal sealed record Saved(
        CardBundleKind Kind,
        Vector3 Position,
        Vector3 Rotation,
        IReadOnlyList<StoredCard> Cards,
        int? Seed);

    // Position and Rotation are typed object, as the card and container records are: a Vector3 cannot be read back
    // into a typed property, so they are read through SavedCardReader.
    private sealed class BundleRecord
    {
        public string Kind { get; set; } = "";
        public object? Position { get; set; }
        public object? Rotation { get; set; }
        public List<SavedStoredItems.Record>? Cards { get; set; }

        // ASSUMPTION (Low): a record written before piles kept a seed has none; it then gets the seed of the cards it
        // starts with, as a new pile does. That is no migration: the game is not released.
        public int? Seed { get; set; }
    }

    /// <summary>The records to write for the decks and piles in <paramref name="tree" />.</summary>
    internal static List<object> Capture(SceneTree tree) =>
        BundlesIn(tree)
            .Select(bundle => (object)new BundleRecord
            {
                Kind = bundle.Kind.ToString(),
                Position = bundle.GlobalPosition,
                Rotation = bundle.GlobalRotation,
                Cards = bundle.Cards.Select(card => SavedStoredItems.RecordOf(card)).ToList(),
                Seed = bundle.PileSeed
            })
            .ToList();

    /// <summary>
    ///     Checks every deck and pile record of a slot and returns them. Throws <see cref="FormatException" /> on the
    ///     first malformed one, so a caller that parses before it frees anything keeps the scene intact.
    /// </summary>
    internal static List<Saved> Parse(List<object> raw)
    {
        var records = SaveExtension.DeserializeObject<List<BundleRecord>>(SaveExtension.SerializeObject(raw))
                      ?? new List<BundleRecord>();
        return records.Select(ParseOne).ToList();
    }

    /// <summary>Frees the scene's decks and piles, then adds each saved one under <paramref name="world" />.</summary>
    internal static void Restore(SceneTree tree, IEnumerable<Saved> saved, Node3D world)
    {
        foreach (var existing in BundlesIn(tree).ToList())
        {
            existing.GetParent()?.RemoveChild(existing);
            existing.QueueFree();
        }

        foreach (var record in saved)
        {
            var bundle = CardBundle.Create(record.Kind, record.Cards, record.Seed);
            world.AddChild(bundle);
            bundle.GlobalPosition = record.Position;
            bundle.GlobalRotation = record.Rotation;
        }
    }

    private static IEnumerable<CardBundle> BundlesIn(SceneTree tree) =>
        tree.GetNodesInGroup(CardBundle.GroupName).OfType<CardBundle>()
            .Where(bundle => bundle.IsInsideTree() && !bundle.IsQueuedForDeletion() && bundle.Count > 0);

    private static Saved ParseOne(BundleRecord record)
    {
        if (!Enum.TryParse<CardBundleKind>(record.Kind, out var kind) || !Enum.IsDefined(kind))
            throw new FormatException($"A card bundle has unknown kind '{record.Kind}'");

        var holder = $"A {kind.ToString().ToLowerInvariant()}";
        if (record.Cards is not { Count: > 0 } cards)
            throw new FormatException($"{holder} holds no cards");

        var position = SavedCardReader.GetVector3FromData(record.Position
            ?? throw new FormatException($"{holder} has no position"));
        var rotation = SavedCardReader.GetVector3FromData(record.Rotation
            ?? throw new FormatException($"{holder} has no rotation"));
        return new Saved(kind, position, rotation,
            cards.Select(card => SavedStoredItems.ParseCard(card, holder)).ToList(), record.Seed);
    }
}
