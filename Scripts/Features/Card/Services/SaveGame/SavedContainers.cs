using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
///     Writes the sealed containers lying in the scene into a slot and rebuilds them from one. Only sealed
///     containers placed from a scene file can be rebuilt; an opened one has spilled its contents.
/// </summary>
internal static class SavedContainers
{
    /// <summary>One sealed container read back from a slot, every field already checked.</summary>
    internal sealed record Saved(string ScenePath, CardContainerKind Kind, ulong? Seed, Vector3 Position, Vector3 Rotation);

    // One sealed container as it is written to a slot. Position and Rotation are typed object, as the card records
    // are: a Vector3 cannot be read back into a typed property, so they are read through SavedCardReader like cards.
    private sealed class ContainerRecord
    {
        public string ScenePath { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? Seed { get; set; }
        public object? Position { get; set; }
        public object? Rotation { get; set; }
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
                Rotation = container.GlobalRotation
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

    /// <summary>Frees the scene's own containers, then adds one container under <paramref name="world" /> per saved one.</summary>
    internal static void Restore(SceneTree tree, IEnumerable<Saved> saved, Node3D world)
    {
        foreach (var existing in tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
                     .Where(container => !string.IsNullOrEmpty(container.SceneFilePath)).ToList())
        {
            existing.QueueFree();
        }

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
            world.AddChild(container);
            // The transform needs the node in the tree, so it is set after AddChild.
            container.GlobalPosition = record.Position;
            container.GlobalRotation = record.Rotation;
        }
    }

    private static IEnumerable<CardContainer> SealedContainersIn(SceneTree tree) =>
        tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(container => !container.IsOpened && !string.IsNullOrEmpty(container.SceneFilePath));

    private static Saved ParseOne(ContainerRecord record)
    {
        if (!Enum.TryParse<CardContainerKind>(record.Kind, out var kind) || !Enum.IsDefined(kind))
            throw new FormatException($"Container from {record.ScenePath} has unknown kind '{record.Kind}'");

        ulong? seed = null;
        if (record.Seed != null)
        {
            if (!ulong.TryParse(record.Seed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                throw new FormatException($"Container from {record.ScenePath} has unreadable seed '{record.Seed}'");
            seed = parsed;
        }

        var position = SavedCardReader.GetVector3FromData(record.Position
            ?? throw new FormatException($"Container from {record.ScenePath} has no position"));
        var rotation = SavedCardReader.GetVector3FromData(record.Rotation
            ?? throw new FormatException($"Container from {record.ScenePath} has no rotation"));
        return new Saved(record.ScenePath, kind, seed, position, rotation);
    }
}
