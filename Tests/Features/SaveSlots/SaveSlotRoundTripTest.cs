using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Card.Services.SaveGame;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
/// Saves two slots that differ in money, card, player position and container, then loads them in the order A, B, A.
/// Each load must bring back exactly its own slot. Both slots share one in-memory file system, and the player is the
/// real <see cref="PlayerController"/> built from the child nodes its _Ready looks for.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SaveSlotRoundTripTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong ContainerSeed = 0xC0FFEEUL;
    private static readonly Vector3 PlayerPosition = new(1f, 2f, 3f);

    [TestCase]
    [TestCategory("Unit")]
    public static async Task EachSlotRestoresItsOwnMoneyCardPlayerAndContainer()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousScene = tree.CurrentScene;
        var spawner = new RecordingSpawner();
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<ICardSpawningService>(spawner);

        var files = new InMemorySlotFileSystem();
        var slots = new ActiveSaveSlot();
        var world = new Node3D { Name = "SlotRoundTripWorld" };
        var money = new MoneyService { Name = "Money" };
        var player = BuildPlayer();
        var card = new CardController
        {
            Name = "SlotCard",
            Signature = new CardSignature(SavedCardFixture.HeldElements),
        };
        var container = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        container.ContentSeed = ContainerSeed;
        container.HasSeed = true;
        // Injected like the autoload's tree injection, so _Ready resolves the registered spawner for the card respawn.
        var service = TestServices.Inject(new GameSaveService(files, slots) { AutoLoadOnStart = false });
        world.AddChild(money);
        world.AddChild(player);
        world.AddChild(card);
        world.AddChild(container);
        world.AddChild(service);
        AddNode(world, autoFree: false);

        try
        {
            tree.CurrentScene = world;
            player.GlobalPosition = PlayerPosition;
            money.TrySpend(380);

            // Slot A: balance 120, one card, the player at PlayerPosition and one sealed container.
            Select(slots, "a");
            service.SaveGame();

            // Slot B: balance 700, no card and no container. The player stays where slot A put it.
            money.Add(580);
            world.RemoveChild(card);
            card.Free();
            world.RemoveChild(container);
            container.Free();
            Select(slots, "b");
            service.SaveGame();

            Select(slots, "a");
            service.LoadGame();
            await NextFrames(world, tree);
            AssertThat(money.Balance).IsEqual(120);
            AssertThat(CardsIn(world)).HasSize(1);
            AssertThat(player.GlobalPosition).IsEqual(PlayerPosition);
            var restored = ContainersIn(tree).Single();
            AssertThat(restored.ContentSeed).IsEqual(ContainerSeed);
            AssertBool(restored.HasSeed).IsTrue();

            Select(slots, "b");
            service.LoadGame();
            await NextFrames(world, tree);
            AssertThat(money.Balance).IsEqual(700);
            AssertThat(CardsIn(world)).HasSize(0);
            AssertThat(ContainersIn(tree)).HasSize(0);

            Select(slots, "a");
            service.LoadGame();
            await NextFrames(world, tree);
            AssertThat(money.Balance).IsEqual(120);
            AssertThat(CardsIn(world)).HasSize(1);
            AssertThat(player.GlobalPosition).IsEqual(PlayerPosition);
            AssertThat(ContainersIn(tree).Single().ContentSeed).IsEqual(ContainerSeed);
        }
        finally
        {
            TearDown(tree, previousScene, world, service);
        }
    }

    /// <summary>The real player, built with the Head, Camera3D and SpotLight3D nodes its _Ready reads.</summary>
    private static PlayerController BuildPlayer()
    {
        var player = new PlayerController { Name = "Player" };
        var head = new Node3D { Name = "Head" };
        var camera = new Camera3D { Name = "Camera3D" };
        camera.AddChild(new SpotLight3D { Name = "SpotLight3D" });
        head.AddChild(camera);
        player.AddChild(head);
        return player;
    }

    private static List<CardController> CardsIn(Node world) =>
        world.GetChildren().OfType<CardController>().ToList();

    private static List<CardContainer> ContainersIn(SceneTree tree) =>
        tree.GetNodesInGroup("card_containers").OfType<CardContainer>().ToList();

    private static void Select(ActiveSaveSlot slots, string raw)
    {
        if (!SaveSlotName.TryCreate(raw, out var name, out var reason))
            throw new InvalidOperationException(reason);

        slots.Select(name);
    }

    // Load defers the card respawn and frees the old containers at the end of the frame: wait for both.
    private static async Task NextFrames(Node world, SceneTree tree)
    {
        await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void TearDown(SceneTree tree, Node? previousScene, Node3D world, GameSaveService service)
    {
        world.RemoveChild(service);
        service.Free();
        world.Free();
        tree.CurrentScene = previousScene;
        TestServices.Reset();
    }

    /// <summary>Adds each respawned card under its parent, as the real spawner does.</summary>
    private sealed class RecordingSpawner : ICardSpawningService
    {
        public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
        {
            var card = new CardController { Signature = signature };
            parent.AddChild(card);
            return card;
        }

        public Node3D? SpawnCard(
            CardSignature signature, int? lookSeed, Transform3D spawnTransform, Node3D parent) =>
            SpawnCard(signature, spawnTransform, parent);

        public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

        public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
    }
}
