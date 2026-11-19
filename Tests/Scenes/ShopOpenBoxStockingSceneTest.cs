using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
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
///     free shelf slot puts the next item from it into that slot. A booster box from a cardboard box fills one product
///     slot; booster packs from a booster box stack four to a slot. A shut box goes onto a storage rack whole.
///     The input service is replaced so the scene's button callbacks are captured and invoked directly; the click
///     target is found by the interaction ray because the camera is aimed at the slot.
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
    private ContainerHolder _holder = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private CardShelf _productShelf = null!;
    private Action<bool>? _leftClick;
    private Action<bool>? _rightPress;
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
        _holder = _player.GetNode<ContainerHolder>("CardInteraction/ContainerHolder");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");
        _productShelf = _shop.GetNode<CardShelf>(ProductShelfPath);

        // The test scene is not the current scene, so the scene-owned services, the stock hand among them, are
        // handed over directly; registered before the shop enters the tree, the slot targets resolve the hand at once.
        ShopSceneServices.RegisterWithCardSpawner(_shop, Seed);
        TestServices.Container.RegisterSingleton<IStockHand>(_player.GetNode<CardHighlighter>("CardInteraction"));

        var input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(input);
        input.When(x => x.RegisterAction(Arg.Any<object>(), "interact", MouseButton.Left, Arg.Any<Action<bool>>()))
            .Do(ci => _leftClick = ci.Arg<Action<bool>>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "card_drop_prepare", MouseButton.Right,
                Arg.Any<Action<bool>>()))
            .Do(ci => _rightPress = ci.Arg<Action<bool>>());

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
    [TestCategory("Unit")]
    public async Task AClickWithAnOpenBoxPutsOneBoosterBoxInTheAimedSlot()
    {
        var box = await CarryOpened("cardboard_box");
        var target = TargetOf(_productShelf, "Slot1");

        await AimAt(target);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Target!.Prompt).IsEqual("Put a booster box here (8 in the box)");
        _leftClick!.Invoke(true);

        AssertThat(target.Slot!.Cards.Count).IsEqual(1);
        AssertThat(((CardContainer)target.Slot.Cards[0]).Kind).IsEqual(CardContainerKind.Pack);
        AssertThat(_holder.Held).IsSame(box);
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task EightClicksEmptyTheBoxOntoTheProductShelfAndKeepTheEmptyBoxInHand()
    {
        var box = await CarryOpened("cardboard_box");

        foreach (var target in _productShelf.SlotTargets.Take(CardContainerLayout.ItemsPerContainer).ToList())
        {
            await AimAt(target);
            AssertBool(ReferenceEquals(_interaction.Target, target))
                .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
                .IsTrue();
            _leftClick!.Invoke(true);
        }

        AssertThat(_productShelf.StockedCount).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertBool(_productShelf.StockedCards.All(c => c is CardContainer { Kind: CardContainerKind.Pack })).IsTrue();
        // The emptied box stays in the hand until the player throws it in a trash can.
        await PhysicsFrames(1);
        AssertThat(_holder.Held).IsSame(box);
        AssertThat(box.Remaining).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AnOpenBoosterBoxStacksFourBoosterPacksInOneSlot()
    {
        var boosterBox = await CarryOpened("pack");
        var target = TargetOf(_productShelf, "Slot1");
        await AimAt(target);
        AssertThat(_interaction.Target!.Prompt).IsEqual("Put a booster pack here (8 in the box)");

        for (var i = 0; i < ShelfPlacementRule.BoostersPerProductSlot; i++)
        {
            AssertBool(ReferenceEquals(_interaction.Target, target))
                .OverrideFailureMessage(
                    $"click {i + 1} aimed at {Describe(_interaction.Target)}, expected {target.Name}")
                .IsTrue();
            _leftClick!.Invoke(true);
            await PhysicsFrames(AimFrames);
        }

        AssertThat(target.Slot!.Cards.Count).IsEqual(ShelfPlacementRule.BoostersPerProductSlot);
        AssertBool(target.Slot.Cards.All(c => c is CardContainer { Kind: CardContainerKind.Booster })).IsTrue();
        AssertBool(target.CanInteract).IsFalse();
        AssertThat(boosterBox.Remaining)
            .IsEqual(CardContainerLayout.ItemsPerContainer - ShelfPlacementRule.BoostersPerProductSlot);
        AssertThat(_holder.Held).IsSame(boosterBox);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AShutBoxGoesOnTheStorageRackWhole()
    {
        var rack = _shop.GetNode<CardShelf>(StorageRackPath);
        var box = Order("cardboard_box");
        await PhysicsFrames(1);
        AssertBool(_holder.TryPickUp(box)).IsTrue();
        // The upper of the rack's two stocked levels, where no board above stands in the way of a standing aim.
        var target = rack.SlotTargets.OrderByDescending(t => t.GlobalPosition.Y).First();

        await AimAt(target);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Target!.Prompt).IsEqual("Put the cardboard box here");
        _leftClick!.Invoke(true);

        AssertBool(_holder.IsHolding).IsFalse();
        AssertBool(target.Slot!.Cards.Contains(box)).IsTrue();
        AssertBool(box.IsOpened).IsFalse();
        AssertBool(box.IsLidOpen).IsFalse();
    }

    // Orders the item, picks it up and opens it with the right button, so it stays in hand with its lid open.
    private async Task<CardContainer> CarryOpened(string itemId)
    {
        var container = Order(itemId);
        await PhysicsFrames(1);
        AssertBool(_holder.TryPickUp(container)).IsTrue();
        _rightPress!.Invoke(true);
        _rightPress.Invoke(false);
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

    private static ShelfSlotTarget TargetOf(CardShelf shelf, string slotName) =>
        shelf.SlotTargets.Single(target => target.Slot!.Name == slotName);

    // Stands the player StandingDistance in front of the target's shelf and turns the camera onto the target, so the
    // interaction ray reaches it the way a player's aim does.
    private async Task AimAt(ShelfSlotTarget target)
    {
        var shelf = (CardShelf)target.GetParent();
        var front = -shelf.GlobalBasis.Z;
        front = new Vector3(front.X, 0f, front.Z).Normalized();
        var standing = target.GlobalPosition + front * StandingDistance;
        _player.GlobalPosition = new Vector3(standing.X, PlayerStandHeight, standing.Z);
        _player.Rotation = Vector3.Zero;
        _interaction.Camera!.LookAt(target.GlobalPosition);
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
