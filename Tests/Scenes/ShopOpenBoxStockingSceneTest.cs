using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     End to end in the shop scene: a carried box opened with the right button stays in hand, and each left click on a
///     shelf level puts the next item from it into the level's next free seat. A booster box from a cardboard box fills
///     one product seat; a booster box emptied into the hand gives eight booster packs that stack four to a seat, filling
///     the level's seats in order wherever on the board the player aims. A shut box goes onto a storage rack whole.
///     The input service is replaced so the scene's button callbacks are captured and invoked directly; the click
///     target is found by the interaction ray because the camera is aimed at a seat.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopOpenBoxStockingSceneTest
{
    private const ulong Seed = 4242;
    private const float StandingDistance = 1.2f;

    // InteractionSystem raycasts once per 0.05 s of physics time, so the aim has to outlast one interval.
    private const int AimFrames = 6;

    // Half the 1.8 m capsule in Scenes/Components/player.tscn: the body stands on the floor.
    private const float PlayerStandHeight = 0.9f;
    private const string ProductShelfPath = "World/Furniture/ProductShelf/Content";
    private const string StorageRackPath = "World/Furniture/StorageRack/Content";

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private InteractionSystem _interaction = null!;
    private IHand _holder = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private CardShelf _productShelf = null!;
    private Action<MouseActionArgs>? _leftClick;
    private Action<MouseActionArgs>? _rightPress;
    private string _optionsPath = null!;

    [BeforeTest]
    public async Task Setup()
    {
        // The shop's pause and options menus resolve OptionsStore when they ready, so the test registers one on its own
        // temp file (the same pattern as OptionsMenuSceneTest) instead of reading the player's settings.
        _optionsPath = $"user://test_shop_open_box_{Guid.NewGuid():N}.cfg";
        TestServices.Container.RegisterSingleton<OptionsStore>(new OptionsStore(_optionsPath));

        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        _holder = _player.GetNode<IHand>("CardInteraction/Hand");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");
        _productShelf = _shop.GetNode<CardShelf>(ProductShelfPath);

        // The test scene is not the current scene, so the scene-owned services are handed over directly, before the
        // shop enters the tree. The level targets find the hand through its group.
        ShopSceneServices.RegisterWithCardSpawner(_shop, Seed);

        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(Arg.Any<object>(), "interact", MouseButton.Left, Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _leftClick = ci.Arg<Action<MouseActionArgs>>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "card_drop_prepare", MouseButton.Right,
                Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _rightPress = ci.Arg<Action<MouseActionArgs>>());

        AddNode(_shop);
        await PhysicsFrames(AimFrames);
    }

    [AfterTest]
    public void Teardown()
    {
        TestServices.Reset();
        if (FileAccess.FileExists(_optionsPath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_optionsPath));
    }

    [TestCase]
    public async Task AClickWithAnOpenBoxPutsOneBoosterBoxInTheLevelsFirstSeat()
    {
        var box = await CarryOpenedBox();
        var (target, seat) = TargetOf(_productShelf, "Slot1");

        await AimAt(seat);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Prompt).IsEqual("Put the booster box here");
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertThat(seat.Cards.Count).IsEqual(1);
        AssertThat(((CardContainer)seat.Cards[0]).Kind).IsEqual(CardContainerKind.Pack);
        AssertThat(_holder.Front).IsSame(box);
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 1);
    }

    [TestCase]
    public async Task EightClicksEmptyTheBoxOntoTheProductShelfAndKeepTheEmptyBoxInHand()
    {
        var box = await CarryOpenedBox();

        // Four booster boxes fill a level; the first two levels take the box's eight.
        foreach (var target in _productShelf.LevelTargets.Take(2).ToList())
        {
            await AimAt(target.Level!.Seats[0]);
            for (var i = 0; i < target.Level.Seats.Count; i++)
            {
                AssertBool(ReferenceEquals(_interaction.Target, target))
                    .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
                    .IsTrue();
                _leftClick!.Invoke(new MouseActionArgs(true, false));
            }
        }

        AssertThat(_productShelf.StockedCount).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertBool(_productShelf.StockedCards.All(c => c is CardContainer { Kind: CardContainerKind.Pack })).IsTrue();
        // The emptied box stays in the hand until the player throws it in a trash can.
        await PhysicsFrames(1);
        AssertThat(_holder.Front).IsSame(box);
        AssertThat(box.Remaining).IsEqual(0);
    }

    [TestCase]
    public async Task EightBoosterPacksPutAnywhereOnALevelStackFourToASeatInOrder()
    {
        var boosterBox = Order("pack");
        await PhysicsFrames(1);
        AssertBool(_holder.TryTake(boosterBox)).IsTrue();
        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));
        AssertThat(_holder.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertBool(GodotObject.IsInstanceValid(boosterBox) && !boosterBox.IsQueuedForDeletion()).IsFalse();
        // Aimed at the level's last seat, which stays free while the first two fill.
        var (target, last) = TargetOf(_productShelf, "Slot4");
        var level = target.Level!;
        await AimAt(last);
        AssertThat(_interaction.Prompt).IsEqual("Put the booster pack here");
        var standing = _player.GlobalPosition;

        for (var i = 0; i < CardContainerLayout.ItemsPerContainer; i++)
        {
            AssertBool(ReferenceEquals(_interaction.Target, target))
                .OverrideFailureMessage(
                    $"click {i + 1} aimed at {Describe(_interaction.Target)}, expected {target.Name}; " +
                    $"level holds {level.Count}, room={level.HasRoom}, " +
                    $"layer={target.CollisionLayer}, prompt={_interaction.Prompt}, " +
                    $"front={(_holder.Front as CardContainer)?.Kind.ToString() ?? "none"} of {_holder.Count}, " +
                    $"camera={_interaction.Camera!.GlobalPosition} facing {-_interaction.Camera.GlobalBasis.Z}, " +
                    $"player={_player.GlobalPosition}")
                .IsTrue();
            _leftClick!.Invoke(new MouseActionArgs(true, false));
            await PhysicsFrames(AimFrames);
            // A stocked booster once got its collision back while it still hung inside the player's capsule, and
            // the player was pushed out of it toward the shelf.
            AssertBool(_player.GlobalPosition.IsEqualApprox(standing))
                .OverrideFailureMessage($"click {i + 1} moved the player from {standing} to {_player.GlobalPosition}")
                .IsTrue();
        }

        AssertThat(level.Seats[0].Cards.Count).IsEqual(ShelfPlacementRule.BoostersPerProductSlot);
        AssertThat(level.Seats[1].Cards.Count).IsEqual(ShelfPlacementRule.BoostersPerProductSlot);
        AssertBool(last.HasCards).IsFalse();
        AssertBool(level.Stock.All(c => c is CardContainer { Kind: CardContainerKind.Booster })).IsTrue();
        AssertThat(_holder.Count).IsEqual(0);
    }

    [TestCase]
    public async Task AShutBoxGoesOnTheStorageRackWhole()
    {
        var rack = _shop.GetNode<CardShelf>(StorageRackPath);
        var box = Order("cardboard_box");
        await PhysicsFrames(1);
        AssertBool(_holder.TryTake(box)).IsTrue();
        // The upper of the rack's two stocked levels, where no board above stands in the way of a standing aim.
        var target = rack.LevelTargets.OrderByDescending(t => t.GlobalPosition.Y).First();

        await AimAt(target.Level!.Seats[0]);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Prompt).IsEqual("Put the cardboard box here");
        _leftClick!.Invoke(new MouseActionArgs(true, false));

        AssertInt(_holder.Count).IsEqual(0);
        AssertBool(target.Level.Stock.Contains(box)).IsTrue();
        AssertBool(box.IsOpened).IsFalse();
        AssertBool(box.IsLidOpen).IsFalse();
    }

    // Orders the cardboard box, picks it up and opens it with the right button, so it stays in hand with its flaps open.
    private async Task<CardContainer> CarryOpenedBox()
    {
        var container = Order("cardboard_box");
        await PhysicsFrames(1);
        AssertBool(_holder.TryTake(container)).IsTrue();
        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));
        AssertBool(container.IsLidOpen).IsTrue();
        return container;
    }

    private CardContainer Order(string itemId)
    {
        var item = Catalog().Items.Single(i => i.Id == itemId);
        _money.Add(item.Price);
        var result = _ordering.Order(item);
        AssertBool(result.Succeeded).IsTrue();
        return (CardContainer)result.Spawned!;
    }

    private OrderCatalog Catalog() =>
        _shop.GetNode<StockApp>("PcDesktopViewport/StockApp").Catalog!;

    // The click target of the level that holds the named seat, and that seat.
    private static (ShelfLevelTarget Target, DeckSlot Seat) TargetOf(CardShelf shelf, string seatName)
    {
        var seat = shelf.Slots.Single(slot => slot.Name == seatName);
        return (shelf.LevelTargets.Single(target => target.Level == shelf.LevelOf(seat)), seat);
    }

    // Stands the player StandingDistance in front of the seat's shelf and turns the camera onto the seat, so the
    // interaction ray reaches the level target there the way a player's aim does.
    private async Task AimAt(DeckSlot seat)
    {
        var shelf = (CardShelf)seat.GetParent().GetParent();
        var front = -shelf.GlobalBasis.Z;
        front = new Vector3(front.X, 0f, front.Z).Normalized();
        var standing = seat.GlobalPosition + front * StandingDistance;
        _player.GlobalPosition = new Vector3(standing.X, PlayerStandHeight, standing.Z);
        _player.Rotation = Vector3.Zero;
        _interaction.Camera!.LookAt(seat.GlobalPosition);
        await PhysicsFrames(AimFrames);
    }

    private static string Describe(IInteractable? target) =>
        target is Node node ? node.Name.ToString() : "nothing";

    private static async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
