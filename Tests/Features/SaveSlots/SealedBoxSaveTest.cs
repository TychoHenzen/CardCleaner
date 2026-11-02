using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
/// Saves a sealed cardboard box, reloads it from the slot, then opens the original and the reloaded box all the way
/// down to their booster cards. Both must spawn the same card signatures. The recording spawner keeps the cards out
/// of the tree, and the in-memory slot file system keeps the test off the user folder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SealedBoxSaveTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const ulong BoxSeed = 0x5EA1EDUL;
    private const int MaxFrames = 300;

    [TestCase]
    [TestCategory("Unit")]
    public static async Task ReloadedBoxOpensIntoTheSameCardSignaturesAsTheOriginal()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousScene = tree.CurrentScene;
        var spawner = new RecordingSpawner();
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<ICardSpawningService>(spawner);

        var slots = new ActiveSaveSlot();
        SelectSlot(slots, "a");
        var world = new Node3D { Name = "SealedBoxWorld" };
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.ContentSeed = BoxSeed;
        box.HasSeed = true;
        box.Spawning = spawner;
        var service = new GameSaveService(new InMemorySlotFileSystem(), slots) { AutoLoadOnStart = false };
        world.AddChild(box);
        world.AddChild(service);
        AddNode(world, autoFree: false);

        try
        {
            tree.CurrentScene = world;
            service.SaveGame();

            // The original box opens on the first frame; the loop opens every container the frames bring in.
            await OpenUntilEmpty(world, tree);
            var original = Canonical(spawner.Signatures);
            AssertThat(original.Count).IsEqual(CardContainerLayout.CardsPerBox);

            spawner.Signatures.Clear();
            service.LoadGame();
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var restored = ContainersIn(tree).Where(c => c.Kind == CardContainerKind.Box).ToList();
            AssertThat(restored).HasSize(1);
            var reloadedBox = restored.Single();
            AssertThat(reloadedBox.ContentSeed).IsEqual(BoxSeed);
            AssertBool(reloadedBox.HasSeed).IsTrue();

            // A restored container has no spawner of its own: the test hands it the recording one, as the shop does.
            reloadedBox.Spawning = spawner;
            await OpenUntilEmpty(world, tree);
            var reopened = Canonical(spawner.Signatures);
            AssertThat(reopened.Count).IsEqual(CardContainerLayout.CardsPerBox);
            AssertBool(reopened.SequenceEqual(original)).IsTrue();
        }
        finally
        {
            TearDown(tree, previousScene, world, service);
        }
    }

    // Opens every closed container in the tree, one frame at a time, until no container is left. A box spawns packs,
    // a pack spawns boosters and a booster spawns cards, so the loop runs until the whole chain has been opened.
    private static async Task OpenUntilEmpty(Node world, SceneTree tree)
    {
        for (var frame = 0; frame < MaxFrames; frame++)
        {
            var containers = ContainersIn(tree);
            if (containers.Count == 0)
                return;

            foreach (var container in containers.Where(c => c.CanInteract))
                container.Open();

            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        throw new InvalidOperationException($"Containers still in the tree after {MaxFrames} frames");
    }

    private static List<CardContainer> ContainersIn(SceneTree tree) =>
        tree.GetNodesInGroup("card_containers").OfType<CardContainer>().ToList();

    // Spawn order follows the tree, so the two boxes are compared as sorted lists of their signatures.
    private static List<string> Canonical(List<CardSignature> signatures) =>
        signatures
            .Select(s => string.Join(",", s.Elements.Select(e => e.ToString("R", CultureInfo.InvariantCulture))))
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

    private static void SelectSlot(ActiveSaveSlot slots, string raw)
    {
        if (!SaveSlotName.TryCreate(raw, out var name, out var reason))
            throw new InvalidOperationException(reason);

        slots.Activate(name);
    }

    private static void TearDown(SceneTree tree, Node? previousScene, Node3D world, GameSaveService service)
    {
        world.RemoveChild(service);
        service.Free();
        world.Free();
        tree.CurrentScene = previousScene;
        TestServices.Reset();
    }

    /// <summary>Records each card a booster spawns. The cards stay out of the tree.</summary>
    private sealed class RecordingSpawner : ICardSpawningService
    {
        public List<CardSignature> Signatures { get; } = [];

        public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
        {
            Signatures.Add(signature);
            return null;
        }

        public Node3D? SpawnCard(
            CardSignature signature, int? lookSeed, Transform3D spawnTransform, Node3D parent) =>
            SpawnCard(signature, spawnTransform, parent);

        public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

        public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
    }
}
