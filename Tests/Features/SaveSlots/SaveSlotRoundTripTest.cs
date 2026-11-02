using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Card.Services.SaveGame;
using CardCleaner.Tests.Features.SaveSlots.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
/// Saves two slots that differ in money, card, player position and container, then loads them in the order A, B, A.
/// The player is moved before every load, so each load must bring back its own slot's position, not the one it was
/// left at. Both slots share one in-memory file system, and the player is the real <see cref="PlayerController"/>
/// built from the child nodes its _Ready looks for.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SaveSlotRoundTripTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong ContainerSeed = 0xC0FFEEUL;
    private const int BalanceA = 120;
    private const int BalanceB = 700;
    private static readonly Vector3 PositionA = new(1f, 2f, 3f);
    private static readonly Vector3 PositionB = new(-4f, 0.5f, 9f);
    private static readonly Vector3 PositionElsewhere = new(30f, 0f, -30f);

    [TestCase]
    [TestCategory("Unit")]
    public static async Task EachSlotRestoresItsOwnMoneyCardPlayerAndContainer()
    {
        using var scene = new SaveWorld("SlotRoundTripWorld");
        var slots = new ActiveSaveSlot();
        var parts = BuildParts(scene, slots);
        AddNode(scene.World, autoFree: false);
        scene.Enter();

        SaveSlotA(scene, slots, parts);
        SaveSlotB(scene, slots, parts);

        await LoadAndAssertSlotA(scene, slots, parts);
        await LoadAndAssertSlotB(scene, slots, parts);
        await LoadAndAssertSlotA(scene, slots, parts);
    }

    private sealed record Parts(
        MoneyService Money,
        PlayerController Player,
        CardController Card,
        CardContainer Container);

    private static Parts BuildParts(SaveWorld scene, ActiveSaveSlot slots)
    {
        var money = new MoneyService { Name = "Money" };
        var player = BuildPlayer();
        var card = new CardController
        {
            Name = "SlotCard",
            Signature = new CardSignature(SavedCardFixture.HeldElements),
        };
        var container = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        container.ContentSeed = ContainerSeed;
        scene.World.AddChild(money);
        scene.World.AddChild(player);
        scene.World.AddChild(card);
        scene.World.AddChild(container);
        scene.Add(new GameSaveService(new InMemorySlotFileSystem(), slots) { AutoLoadOnStart = false });
        return new Parts(money, player, card, container);
    }

    // Slot A: balance 120, one card, the player at PositionA and one sealed container.
    private static void SaveSlotA(SaveWorld scene, ActiveSaveSlot slots, Parts parts)
    {
        parts.Player.GlobalPosition = PositionA;
        parts.Money.TrySpend(parts.Money.StartingBalance - BalanceA);
        SaveWorld.Activate(slots, "a");
        scene.Service!.SaveGame();
    }

    // Slot B: balance 700, the player at PositionB, no card and no container.
    private static void SaveSlotB(SaveWorld scene, ActiveSaveSlot slots, Parts parts)
    {
        parts.Money.Add(BalanceB - BalanceA);
        parts.Player.GlobalPosition = PositionB;
        scene.World.RemoveChild(parts.Card);
        parts.Card.Free();
        scene.World.RemoveChild(parts.Container);
        parts.Container.Free();
        SaveWorld.Activate(slots, "b");
        scene.Service!.SaveGame();
    }

    private static async Task LoadAndAssertSlotA(SaveWorld scene, ActiveSaveSlot slots, Parts parts)
    {
        await Load(scene, slots, parts, "a");
        AssertThat(parts.Money.Balance).IsEqual(BalanceA);
        AssertThat(parts.Player.GlobalPosition).IsEqual(PositionA);
        AssertThat(CardsIn(scene.World)).HasSize(1);
        AssertThat(ContainersIn(scene.Tree).Single().ContentSeed).IsEqual(ContainerSeed);
    }

    private static async Task LoadAndAssertSlotB(SaveWorld scene, ActiveSaveSlot slots, Parts parts)
    {
        await Load(scene, slots, parts, "b");
        AssertThat(parts.Money.Balance).IsEqual(BalanceB);
        AssertThat(parts.Player.GlobalPosition).IsEqual(PositionB);
        AssertThat(CardsIn(scene.World)).HasSize(0);
        AssertThat(ContainersIn(scene.Tree)).HasSize(0);
    }

    // The player is moved away first, so the position can only come from the slot being loaded.
    private static async Task Load(SaveWorld scene, ActiveSaveSlot slots, Parts parts, string slot)
    {
        parts.Player.GlobalPosition = PositionElsewhere;
        SaveWorld.Activate(slots, slot);
        scene.Service!.LoadGame();
        // Load defers the card respawn and frees the old containers at the end of the frame: wait for both.
        await scene.TwoFrames();
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
}
