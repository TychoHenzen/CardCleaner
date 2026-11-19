using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;
using Newtonsoft.Json;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
///     Writes the sealed containers lying in the scene into a slot and rebuilds them from one. Only sealed
///     containers placed from a scene file can be rebuilt; an opened one has spilled its contents. A carried
///     container is saved with a held flag and handed back to the player's hand on restore, in the order the hand held
///     it, so a stack of booster packs comes back in the same order. A cardboard box keeps its open flaps and what it
///     holds (cards, boosters and booster boxes, each with its seed and price, in the order they went in), and every
///     container the price the player set on it.
/// </summary>
internal static class SavedContainers
{
    /// <summary>One sealed container read back from a slot, every field already checked.</summary>
    internal sealed record Saved(
        string ScenePath,
        CardContainerKind Kind,
        ulong? Seed,
        Vector3 Position,
        Vector3 Rotation,
        bool Held = false,
        bool LidOpen = false,
        int? Price = null,
        IReadOnlyList<StoredItem>? Contents = null);

    // One sealed container as it is written to a slot. Position and Rotation are typed object, as the card records
    // are: a Vector3 cannot be read back into a typed property, so they are read through SavedCardReader like cards.
    private sealed class ContainerRecord
    {
        public string ScenePath { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? Seed { get; set; }
        public object? Position { get; set; }
        public object? Rotation { get; set; }
        public bool Held { get; set; }
        public bool LidOpen { get; set; }
        public object? Price { get; set; }

        // What a cardboard box holds, in the order it went in; null for a fresh box, which still holds what it came
        // with (drawn from its seed), and for every other container.
        public List<SavedStoredItems.Record>? Contents { get; set; }
    }

    /// <summary>
    ///     Ends every booster opening in a hand in <paramref name="tree" />
    ///     (<see cref="CardContainer.FinishOpening" />), so a save finds every card in the hand instead of losing the
    ///     ones still inside the opening booster.
    /// </summary>
    internal static void FinishOpenings(SceneTree tree)
    {
        foreach (var container in tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
                     .Where(container => container.IsOpened).ToList())
            container.FinishOpening();
    }

    /// <summary>The records to write for the sealed containers in <paramref name="tree" />.</summary>
    internal static List<object> Capture(SceneTree tree)
    {
        return SealedContainersIn(tree)
            .Select(container => (object)new ContainerRecord
            {
                ScenePath = container.SceneFilePath,
                Kind = container.Kind.ToString(),
                Seed = container.SeedForSave()?.ToString(CultureInfo.InvariantCulture),
                Position = container.GlobalPosition,
                Rotation = container.GlobalRotation,
                Held = container.IsHeld,
                LidOpen = container.IsLidOpen,
                Price = container.ShopPrice,
                Contents = container.Storage?.SavedItems?.Select(SavedStoredItems.RecordOf).ToList()
            })
            .ToList();
    }

    /// <summary>
    ///     Checks every container record of a slot and returns them. Throws <see cref="FormatException" /> on the
    ///     first malformed one, so a caller that parses before it frees anything keeps the scene intact.
    /// </summary>
    internal static List<Saved> Parse(List<object> raw)
    {
        var records = SaveExtension.DeserializeObject<List<ContainerRecord>>(SaveExtension.SerializeObject(raw))
                      ?? new List<ContainerRecord>();
        return records.Select(ParseOne).ToList();
    }

    /// <summary>
    ///     Returns the container records of a slot with each readable position passed through <paramref name="move" />.
    ///     A null result, or a position that cannot be read, keeps the record as it was, so <see cref="Parse" /> still
    ///     reports a malformed one.
    /// </summary>
    internal static List<object> Relocate(List<object> raw, Func<Vector3, Vector3?> move)
    {
        var records = SaveExtension.DeserializeObject<List<ContainerRecord>>(SaveExtension.SerializeObject(raw))
                      ?? new List<ContainerRecord>();
        foreach (var record in records)
        {
            if (TryReadPosition(record) is { } position && move(position) is { } moved)
                record.Position = moved;
        }

        return records.Cast<object>().ToList();
    }

    private static Vector3? TryReadPosition(ContainerRecord record)
    {
        if (record.Position == null)
            return null;

        try
        {
            return SavedCardReader.GetVector3FromData(record.Position);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Frees the scene's own containers, then adds one container under <paramref name="world" /> per saved one, and
    ///     locks each one saved on a slot's seat back into that slot.
    /// </summary>
    internal static void Restore(SceneTree tree, IEnumerable<Saved> saved, Node3D world)
    {
        // ASSUMPTION: one player has one hand (Risk Low), so only the first holder in the group is used. The records
        // come in tree order, which is the order the hand picked its containers up, so a stack keeps its order.
        var holder = tree.GetNodesInGroup(ContainerHolder.GroupName).OfType<IContainerHolder>().FirstOrDefault();

        foreach (var existing in tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
                     .Where(container => !string.IsNullOrEmpty(container.SceneFilePath)).ToList())
        {
            // RemoveChild leaves the tree at once, so a held one is released now, not when Godot frees it.
            existing.GetParent()?.RemoveChild(existing);
            existing.QueueFree();
        }

        var slots = SlotsUnder(world).ToList();
        var seated = new List<(CardContainer Container, DeckSlot Slot, int Seat)>();
        foreach (var record in saved)
        {
            var packed = ResourceLoader.Load<PackedScene>(record.ScenePath);
            if (packed?.Instantiate() is not CardContainer container)
            {
                ILog.Error($"Could not restore container from {record.ScenePath}");
                continue;
            }

            // The seed is set before AddChild, so the container never draws its own.
            container.Kind = record.Kind;
            container.ContentSeed = record.Seed;
            container.Storage?.Restore(record.Contents);
            // Before the slot lock below, so the slot's price tag shows this price when it repaints.
            container.ShopPrice = record.Price;
            world.AddChild(container);
            // The transform needs the node in the tree, so it is set after AddChild, and so does the lid, whose parts
            // take their shut pose in their own _Ready.
            container.GlobalPosition = record.Position;
            container.GlobalRotation = record.Rotation;
            if (record.LidOpen)
                container.SetLidOpen(true, instantly: true);
            // A failed pick-up leaves the container at its saved position.
            if (record.Held) holder?.TryPickUp(container);
            else if (SeatOf(slots, record.Position) is { } seat) seated.Add((container, seat.Slot, seat.Index));
        }

        // Locked now, before any physics step, not by the slot's trigger: a seat lies inside its shelf's solid
        // collider, which pushes a free container off the seat, and out of the trigger, on the first step. A slot's
        // lower seats are filled first, so a stack comes back in its saved order.
        foreach (var (container, slot, _) in seated.OrderBy(entry => entry.Seat))
            slot.TryLock(container);
    }

    private static (DeckSlot Slot, int Index)? SeatOf(IEnumerable<DeckSlot> slots, Vector3 position)
    {
        foreach (var slot in slots)
        {
            if (slot.SeatAt(position) is { } index)
                return (slot, index);
        }

        return null;
    }

    private static IEnumerable<DeckSlot> SlotsUnder(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is DeckSlot slot)
                yield return slot;

            foreach (var nested in SlotsUnder(child))
                yield return nested;
        }
    }

    private static IEnumerable<CardContainer> SealedContainersIn(SceneTree tree) =>
        tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(container => !container.IsOpened && !string.IsNullOrEmpty(container.SceneFilePath));

    private static Saved ParseOne(ContainerRecord record)
    {
        var kind = ParseKind(record);
        var seed = ParseSeed(record);
        var position = SavedCardReader.GetVector3FromData(record.Position
            ?? throw new FormatException($"Container from {record.ScenePath} has no position"));
        var rotation = SavedCardReader.GetVector3FromData(record.Rotation
            ?? throw new FormatException($"Container from {record.ScenePath} has no rotation"));
        return new Saved(record.ScenePath, kind, seed, position, rotation, record.Held, record.LidOpen,
            SavedStoredItems.ParsePrice(record.Price, "container"), ParseContents(record, kind));
    }

    // Only a cardboard box holds stored items; every item must be readable, or the record is malformed.
    private static List<StoredItem>? ParseContents(ContainerRecord record, CardContainerKind kind)
    {
        if (record.Contents == null)
            return null;

        if (kind != CardContainerKind.Box)
            throw new FormatException($"Container from {record.ScenePath} is a {kind} and cannot hold stored items");

        return record.Contents.Select(item => SavedStoredItems.Parse(item, $"Cardboard box from {record.ScenePath}"))
            .ToList();
    }

    private static CardContainerKind ParseKind(ContainerRecord record)
    {
        if (Enum.TryParse<CardContainerKind>(record.Kind, out var kind) && Enum.IsDefined(kind))
            return kind;

        throw new FormatException($"Container from {record.ScenePath} has unknown kind '{record.Kind}'");
    }

    private static ulong? ParseSeed(ContainerRecord record)
    {
        if (record.Seed == null)
            return null;

        if (ulong.TryParse(record.Seed, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
            return seed;

        throw new FormatException($"Container from {record.ScenePath} has unreadable seed '{record.Seed}'");
    }
}
