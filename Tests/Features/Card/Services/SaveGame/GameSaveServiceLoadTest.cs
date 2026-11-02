using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Card.Services.SaveGame;

/// <summary>
/// Loads the committed pre-move save through the real save system (#172 U1). The game save service respawns each saved
/// card, and the held card is handed to the player camera. The service reads and writes an in-memory slot file system,
/// so no test touches the user folder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSaveServiceLoadTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static async Task LoadingTheSaveRespawnsEachCardAndHandsTheHeldOneToTheCamera()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousScene = tree.CurrentScene;
        var spawner = new RecordingSpawner();
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<ICardSpawningService>(spawner);

        var activeSlot = SelectSlot("a");
        var files = new InMemorySlotFileSystem();
        var envelope = SaveEnvelope.Write(SavedCardFixture.ReadText(), DateTime.UtcNow);
        files.Seed(SaveSlotLayout.FileFor(activeSlot.Current), envelope);

        var world = new Node3D { Name = "GameSaveLoadWorld" };
        var player = new Node3D { Name = "Player" };
        var head = new Node3D { Name = "Head" };
        var camera = new Camera3D { Name = "Camera3D" };
        player.AddToGroup("player");
        head.AddChild(camera);
        player.AddChild(head);
        var service = TestServices.Inject(new GameSaveService(files, activeSlot) { AutoLoadOnStart = false });
        world.AddChild(player);
        world.AddChild(service);
        AddNode(world, autoFree: false);

        try
        {
            tree.CurrentScene = world;
            service.LoadGame();

            // Load defers RecreateCards, and RecreateCards defers the held card's reparent: wait for both.
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(spawner.Spawned).HasSize(2);
            var held = spawner.Spawned[0];
            AssertThat(held.Signature.Elements).IsEqual(SavedCardFixture.HeldElements);
            AssertThat(held.Transform.Origin).IsEqual(SavedCardFixture.HeldPosition);
            AssertThat(held.Transform.Basis).IsEqual(Basis.FromEuler(SavedCardFixture.HeldRotation));
            AssertObject(held.Card.GetParent()).IsSame(camera);
            AssertThat(held.LookSeed).IsNull();

            var placed = spawner.Spawned[1];
            AssertThat(placed.Signature.Elements).IsEqual(SavedCardFixture.PlacedElements);
            AssertThat(placed.Transform.Origin).IsEqual(SavedCardFixture.PlacedPosition);
            AssertThat(placed.Transform.Basis).IsEqual(Basis.FromEuler(SavedCardFixture.PlacedRotation));
            AssertObject(placed.Card.GetParent()).IsSame(world);
            AssertThat(placed.LookSeed).IsNull();
        }
        finally
        {
            // GameSaveService._ExitTree saves to the in-memory slot. Removing the service first runs that save while
            // the cards are still in the tree.
            TearDown(tree, previousScene, world, service);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task SavingAndLoadingAnAbsentSignaturePreservesAbsence()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousScene = tree.CurrentScene;
        var spawner = new RecordingSpawner();
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<ICardSpawningService>(spawner);

        var card = new CardController { Name = "AbsentCard" };
        var world = new Node3D { Name = "GameSaveAbsentWorld" };
        var service = TestServices.Inject(
            new GameSaveService(new InMemorySlotFileSystem(), SelectSlot("a")) { AutoLoadOnStart = false });
        world.AddChild(card);
        world.AddChild(service);
        AddNode(world, autoFree: false);

        try
        {
            tree.CurrentScene = world;
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var save = new NodeSave();
            service.Save(save);
            AssertBool(save.TryGetProperty<List<object>>("cards", out var entries)).IsTrue();
            AssertThat(entries).HasSize(1);

            service.Load(save);
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(spawner.Spawned).HasSize(1);
            AssertBool(spawner.Spawned[0].Card.HasSignature).IsFalse();
        }
        finally
        {
            TearDown(tree, previousScene, world, service);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task SavingAndLoadingALookSeedHandsItToTheSpawner()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousScene = tree.CurrentScene;
        var spawner = new RecordingSpawner();
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<ICardSpawningService>(spawner);

        var world = new Node3D { Name = "GameSaveLookWorld" };
        var withLook = new CardController { Name = "WithLook", Signature = new CardSignature(), LookSeed = 4242 };
        var withoutLook = new CardController { Name = "WithoutLook", Signature = new CardSignature() };
        var service = TestServices.Inject(
            new GameSaveService(new InMemorySlotFileSystem(), SelectSlot("a")) { AutoLoadOnStart = false });
        world.AddChild(withLook);
        world.AddChild(withoutLook);
        world.AddChild(service);
        AddNode(world, autoFree: false);

        try
        {
            tree.CurrentScene = world;
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var save = new NodeSave();
            service.Save(save);
            spawner.Spawned.Clear();
            service.Load(save);
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(spawner.Spawned).HasSize(2);
            var seeds = spawner.Spawned.Select(s => s.LookSeed).ToList();
            AssertBool(seeds.Count(v => v is null) == 1).IsTrue();
            AssertThat(seeds.Single(v => v.HasValue)!.Value).IsEqual(4242);
        }
        finally
        {
            TearDown(tree, previousScene, world, service);
        }
    }

    private static ActiveSaveSlot SelectSlot(string raw)
    {
        if (!SaveSlotName.TryCreate(raw, out var name, out var reason))
            throw new InvalidOperationException(reason);

        var slot = new ActiveSaveSlot();
        slot.Activate(name);
        return slot;
    }

    private static void TearDown(SceneTree tree, Node? previousScene, Node3D world, GameSaveService service)
    {
        world.RemoveChild(service);
        service.Free();
        world.Free();
        tree.CurrentScene = previousScene;
        TestServices.Reset();
    }

    /// <summary>Records each respawn and adds the card under its parent, as the real spawner does.</summary>
    private sealed class RecordingSpawner : ICardSpawningService
    {
        public List<Spawn> Spawned { get; } = [];

        public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent) =>
            SpawnCard(signature, null, spawnTransform, parent);

        public Node3D? SpawnCard(
            CardSignature signature, int? lookSeed, Transform3D spawnTransform, Node3D parent)
        {
            var card = new CardController { Signature = signature };
            parent.AddChild(card);
            Spawned.Add(new Spawn(signature, spawnTransform, card, lookSeed));
            return card;
        }

        public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

        public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
    }

    private sealed record Spawn(
        CardSignature Signature,
        Transform3D Transform,
        CardController Card,
        int? LookSeed = null);
}
