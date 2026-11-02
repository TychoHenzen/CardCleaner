using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
/// Saves a carried sealed box and restores it through <see cref="SavedContainers" /> with a
/// <see cref="ContainerHolder" /> in the world, the way the player's hand has one. Covers the held flag, the fallbacks
/// to the floor, a restore over a carried container, and an old slot that has no held key. The recording spawner
/// keeps cards out of the tree.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HeldContainerSaveTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const ulong BoxSeed = 0x4E1DUL;

    [TestCase]
    [TestCategory("Unit")]
    public static async Task SaveWhileHeld_RestoresHeldAndUnopened()
    {
        using var scene = new SaveWorld("HeldSaveWorld", addCardsToTree: false);
        Assertions.AddNode(scene.World, autoFree: false);
        scene.Enter();
        var (holder, hand) = BuildHolder(scene);
        var box = PlacedBox(scene, Vector3.Zero);
        AssertBool(holder.TryPickUp(box)).IsTrue();

        SavedContainers.Restore(scene.Tree, CaptureAndParse(scene), scene.World);
        await scene.TwoFrames();

        var restored = OnlyRestoredBox(scene);
        AssertBool(holder.Held == restored).IsTrue();
        AssertBool(restored.IsOpened).IsFalse();
        AssertBool(restored.GetParent() == hand).IsTrue();
        AssertThat(restored.CollisionLayer).IsEqual(0u);
        AssertThat(restored.ContentSeed).IsEqual(BoxSeed);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task SaveOnFloor_RestoresOnFloor()
    {
        using var scene = new SaveWorld("FloorSaveWorld", addCardsToTree: false);
        Assertions.AddNode(scene.World, autoFree: false);
        scene.Enter();
        var (holder, _) = BuildHolder(scene);
        var position = new Vector3(2f, 0f, 3f);
        PlacedBox(scene, position);

        var records = CaptureAndParse(scene);
        AssertBool(records.Single().Held).IsFalse();
        SavedContainers.Restore(scene.Tree, records, scene.World);

        // Checked before any frame runs, so physics has not moved the box off its saved position.
        var restored = OnlyRestoredBox(scene);
        AssertBool(holder.IsHolding).IsFalse();
        AssertBool(restored.GetParent() == scene.World).IsTrue();
        AssertThat(restored.GlobalPosition).IsEqual(position);
        AssertThat(restored.ContentSeed).IsEqual(BoxSeed);

        // Restore queue-frees the scene's own box; a frame lets Godot free it before the test ends.
        await scene.TwoFrames();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RestoreHeld_WithoutHolder_FallsBackToPosition()
    {
        using var scene = new SaveWorld("NoHolderWorld", addCardsToTree: false);
        Assertions.AddNode(scene.World, autoFree: false);
        scene.Enter();
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            var position = new Vector3(-1f, 0f, 4f);
            var held = new SavedContainers.Saved(
                BoxScene, CardContainerKind.Box, BoxSeed, position, Vector3.Zero, true);
            SavedContainers.Restore(scene.Tree, new[] { held }, scene.World);

            var restored = OnlyRestoredBox(scene);
            AssertBool(restored.IsHeld).IsFalse();
            AssertThat(restored.GlobalPosition).IsEqual(position);
            // The restored box has no injected resolver in this world, so its own _Ready reports that. Nothing else may
            // be logged: the missing holder is not an error.
            AssertThat(log.Errors.Where(e => !e.EndsWith("was not injected", StringComparison.Ordinal)).ToList())
                .IsEmpty();
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RestoreHeld_WhenHolderBusy_FallsBackToPosition()
    {
        using var scene = new SaveWorld("BusyHolderWorld", addCardsToTree: false);
        Assertions.AddNode(scene.World, autoFree: false);
        scene.Enter();
        var (holder, _) = BuildHolder(scene);
        // A code-built occupant has no scene file, so Restore leaves it in the hand.
        var occupant = new CardContainer();
        scene.World.AddChild(occupant);
        AssertBool(holder.TryPickUp(occupant)).IsTrue();

        var position = new Vector3(-1f, 0f, 4f);
        var held = new SavedContainers.Saved(BoxScene, CardContainerKind.Box, BoxSeed, position, Vector3.Zero, true);
        SavedContainers.Restore(scene.Tree, new[] { held }, scene.World);

        AssertBool(holder.Held == occupant).IsTrue();
        var restored = OnlyRestoredBox(scene);
        AssertBool(restored.GetParent() == scene.World).IsTrue();
        AssertBool(restored.GlobalPosition.DistanceTo(position) < 0.01f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task RestoreOverCarriedContainer_ReleasesHolder()
    {
        using var scene = new SaveWorld("ReleaseHolderWorld", addCardsToTree: false);
        Assertions.AddNode(scene.World, autoFree: false);
        scene.Enter();
        var (holder, _) = BuildHolder(scene);
        // A scene-instanced box in the hand: Restore frees it, which must release the hand at once.
        var carried = PlacedBox(scene, Vector3.Zero);
        AssertBool(holder.TryPickUp(carried)).IsTrue();

        SavedContainers.Restore(scene.Tree, Array.Empty<SavedContainers.Saved>(), scene.World);

        AssertBool(holder.IsHolding).IsFalse();
        AssertThat(holder.Held).IsNull();

        // Restore queue-frees the carried box; a frame lets Godot free it before the test ends.
        await scene.TwoFrames();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OldSlotWithoutHeldKey_ReadsAsFloor()
    {
        // A record written before the held flag existed: no Held key and no Seed key.
        var raw = new List<object>
        {
            new Dictionary<string, object>
            {
                ["ScenePath"] = BoxScene,
                ["Kind"] = "Box",
                ["Position"] = new Vector3(1f, 0f, 2f),
                ["Rotation"] = Vector3.Zero,
            },
        };

        var records = SavedContainers.Parse(raw);

        AssertThat(records).HasSize(1);
        AssertThat(records[0].ScenePath).IsEqual(BoxScene);
        AssertBool(records[0].Held).IsFalse();
    }

    // A holder in the world with a hand anchor the way the player's camera provides one, built with the test services.
    private static HolderRig BuildHolder(SaveWorld scene)
    {
        var hand = new Camera3D { Name = "HandAnchor" };
        scene.World.AddChild(hand);
        var holder = TestServices.Inject(new ContainerHolder());
        scene.World.AddChild(holder);
        holder.SetReferences(hand);
        return new HolderRig(holder, hand);
    }

    private sealed record HolderRig(ContainerHolder Holder, Camera3D Hand);

    private static CardContainer PlacedBox(SaveWorld scene, Vector3 position)
    {
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.ContentSeed = BoxSeed;
        box.Spawning = scene.Spawner;
        scene.World.AddChild(box);
        box.GlobalPosition = position;
        return box;
    }

    private static List<SavedContainers.Saved> CaptureAndParse(SaveWorld scene) =>
        SavedContainers.Parse(SavedContainers.Capture(scene.Tree));

    // The one box Restore added: scene-instanced boxes in the group, so an occupant built in code is not counted.
    private static CardContainer OnlyRestoredBox(SaveWorld scene)
    {
        var boxes = scene.Tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(container => container.SceneFilePath == BoxScene)
            .ToList();
        AssertThat(boxes).HasSize(1);
        return boxes.Single();
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = new();

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogError(string message) => Errors.Add(message);

        public void LogError(string message, Exception exception) => Errors.Add(message);
    }
}
