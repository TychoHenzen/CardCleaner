using System;
using System.Collections.Generic;
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
using CardCleaner.Tests.Features.Packs.Components;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     End to end in the shop scene: booster packs put on a product-shelf level one click at a time while more are held
///     each land whole, sealed and with their own seed, in the level's next free seat, and a booster pack taken off a
///     level with an aimed click opens in the hand and gives its eight cards, whether it topped a full stack or a
///     part-filled one, came out of a booster box emptied into the hand (seeded from the box), or was put there by the
///     restocker. A take always removes the level's last booster, whichever booster of the stack the player aims at.
///     Items move between the hand and a level as records, so a booster on the shelf is a new body with the seed of the
///     one that left the hand. The input service is replaced so the scene's button callbacks are captured and invoked
///     directly; the click target is found by the interaction ray because the camera is aimed at a seat.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopShelfBoosterOpeningSceneTest
{
    private const ulong Seed = 4343;
    private const float StandingDistance = 1.2f;

    // InteractionSystem raycasts once per 0.05 s of physics time, so the aim has to outlast one interval.
    private const int AimFrames = 6;

    // Half the 1.8 m capsule in Scenes/Components/player.tscn: the body stands on the floor.
    private const float PlayerStandHeight = 0.9f;

    // CardContainer's interaction layer bit (layer 3), which a booster on a shelf keeps.
    private const uint InteractableLayerBit = 4;

    private const string ProductShelfPath = "World/Furniture/ProductShelf/Content";

    // The seat of the level the tests stock: its first seat, and its last, which stays free while the first two fill.
    private const string FirstSeat = "Slot1";
    private const string LastSeat = "Slot4";

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private InteractionSystem _interaction = null!;
    private IHand _hand = null!;
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
        // temp file instead of reading the player's settings.
        _optionsPath = $"user://test_shop_shelf_booster_{Guid.NewGuid():N}.cfg";
        TestServices.Container.RegisterSingleton<OptionsStore>(new OptionsStore(_optionsPath));

        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        _hand = _player.GetNode<IHand>("CardInteraction/Hand");
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
        await BoundedWait.SyncPhysicsFrames(AimFrames);
    }

    [AfterTest]
    public void Teardown()
    {
        TestServices.Reset();
        if (FileAccess.FileExists(_optionsPath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_optionsPath));
    }

    // The reported bug: putting booster packs on a shelf while holding more of them broke them. A booster handed from
    // the hand to a seat got its collision back while it still hung at the camera, inside the player's capsule, so the
    // player's next physics step pushed the player 0.36 m toward the shelf and the next click landed elsewhere. The
    // hand now gives a booster up as a record, and the level builds it on its seat, out of the player's way.
    [TestCase]
    public async Task PuttingBoostersOnAShelfWhileHoldingBoostersPutsEachWholeInTheLevelsNextSeat()
    {
        var boosterBox = Order("pack");
        await BoundedWait.SyncPhysicsFrames(1);
        AssertBool(_hand.TryTake(boosterBox)).IsTrue();
        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));
        AssertThat(_hand.Count).IsEqual(CardContainerLayout.ItemsPerContainer);

        // Four fill the first seat's stack and two more go into the second: every click is made with boosters still in
        // the hand, aimed at the level's last seat.
        var (target, last) = TargetOf(LastSeat);
        var level = target.Level!;
        await AimAt(last.GlobalPosition, last);
        var standing = _player.GlobalPosition;
        const int clicks = ShelfPlacementRule.BoostersPerProductSlot + 2;
        for (var placed = 1; placed <= clicks; placed++)
        {
            AssertBool(ReferenceEquals(_interaction.Target, target))
                .OverrideFailureMessage(
                    $"click {placed} aimed at {Describe(_interaction.Target)}, expected {target.Name}; " +
                    $"player at {_player.GlobalPosition}, stood at {standing}")
                .IsTrue();
            // Boosters out of an unseeded booster box draw their seed when one is asked for, as the move does.
            var seed = ((CardContainer)_hand.Front!).SeedForSave();
            _leftClick!.Invoke(new MouseActionArgs(true, false));
            await BoundedWait.SyncPhysicsFrames(AimFrames);

            AssertBool(_player.GlobalPosition.IsEqualApprox(standing))
                .OverrideFailureMessage($"placing moved the player from {standing} to {_player.GlobalPosition}")
                .IsTrue();
            AssertThat(level.Count).IsEqual(placed);
            var booster = (CardContainer)level.Last!;
            var seat = (DeckSlot)booster.GetParent();
            var stackIndex = (placed - 1) % ShelfPlacementRule.BoostersPerProductSlot;
            AssertThat(seat).IsSame(level.Seats[(placed - 1) / ShelfPlacementRule.BoostersPerProductSlot]);
            AssertBool(booster.GlobalPosition.IsEqualApprox(seat.SeatPosition(stackIndex))).IsTrue();
            AssertBool(booster.IsOpened).IsFalse();
            AssertBool(booster.Freeze).IsTrue();
            // Compared as a bool: gdUnit binds a nullable expected value to no overload of the ulong assert.
            AssertBool(booster.ContentSeed == seed)
                .OverrideFailureMessage($"the shelf booster has seed {booster.ContentSeed}, the held one had {seed}")
                .IsTrue();
            AssertThat(booster.CollisionLayer & InteractableLayerBit).IsEqual(InteractableLayerBit);

            // The boosters still in hand are untouched: held, out of the physics world, and sealed.
            AssertThat(_hand.Count).IsEqual(CardContainerLayout.ItemsPerContainer - placed);
            foreach (var held in _hand.Items.OfType<CardContainer>())
            {
                AssertBool(held.IsHeld && !held.IsOpened).IsTrue();
                AssertThat(held.CollisionLayer).IsEqual(0u);
            }
        }

        AssertBool(last.HasCards).IsFalse();
    }

    [TestCase]
    public async Task TheTopBoosterOfAFullStackOpensInTheHand()
    {
        var level = await StockByHand(ShelfPlacementRule.BoostersPerProductSlot);

        await TakeOffAndOpen(level, level.Seats[0], ((CardContainer)level.Last!).ContentSeed);
    }

    [TestCase]
    public async Task TheTopBoosterOfAPartFilledStackOpensInTheHand()
    {
        var level = await StockByHand(2);

        await TakeOffAndOpen(level, level.Seats[0], ((CardContainer)level.Last!).ContentSeed);
    }

    // Aimed at the bottom of a full stack, the click still takes the booster put on last, so the rest stays put.
    [TestCase]
    public async Task AClickAimedLowOnAFullStackTakesItsTopBoosterAndItOpensInTheHand()
    {
        var level = await StockByHand(ShelfPlacementRule.BoostersPerProductSlot);
        var top = (CardContainer)level.Last!;
        var below = level.Stock.Take(level.Count - 1).ToList();

        await TakeOffAndOpen(level, level.Seats[0], top.ContentSeed);

        AssertBool(level.Stock.SequenceEqual(below)).IsTrue();
    }

    [TestCase]
    public async Task ABoosterFromABoosterBoxEmptiedIntoTheHandOpensAfterAShelfRound()
    {
        var boosterBox = Order("pack");
        await BoundedWait.SyncPhysicsFrames(1);
        AssertBool(_hand.TryTake(boosterBox)).IsTrue();
        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));
        AssertThat(_hand.Count).IsEqual(CardContainerLayout.ItemsPerContainer);

        var (target, last) = TargetOf(LastSeat);
        await AimAt(last.GlobalPosition, last);
        for (var i = 0; i < ShelfPlacementRule.BoostersPerProductSlot; i++)
        {
            _leftClick!.Invoke(new MouseActionArgs(true, false));
            await BoundedWait.SyncPhysicsFrames(AimFrames);
        }

        var level = target.Level!;
        AssertThat(level.Count).IsEqual(ShelfPlacementRule.BoostersPerProductSlot);
        await TakeOffAndOpen(level, level.Seats[0], ((CardContainer)level.Last!).ContentSeed);
    }

    [TestCase]
    public async Task ABoosterTheRestockerStockedOpensInTheHand()
    {
        var booster = Order("booster");
        await BoundedWait.SyncPhysicsFrames(1);
        var (target, _) = TargetOf(FirstSeat);
        var level = target.Level!;

        // The restocker's own hands: it takes the booster off the floor, carries it off every layer, and locks it in.
        var restocker = new Node3D { Name = "TestRestocker" };
        var carry = new Node3D { Name = "Hand" };
        restocker.AddChild(carry);
        _shop.GetNode("World").AddChild(restocker);
        var hands = new RestockerHands(restocker, carry);
        AssertBool(hands.Take(booster)).IsTrue();
        AssertBool(hands.Shelve(level)).IsTrue();
        await BoundedWait.SyncPhysicsFrames(AimFrames);

        // The restocker moves the body itself; an ordered booster draws its seed when it leaves the shelf.
        await TakeOffAndOpen(level, level.Seats[0], booster.ContentSeed);
    }

    // Orders `count` booster packs, takes them into the hand and clicks them one at a time onto the level of the
    // product shelf's first seat, aimed at its last seat, the way a player stocks a board.
    private async Task<ShelfLevel> StockByHand(int count)
    {
        var boosters = new List<CardContainer>();
        for (var i = 0; i < count; i++)
            boosters.Add(Order("booster"));
        await BoundedWait.SyncPhysicsFrames(1);
        foreach (var booster in boosters)
            AssertBool(_hand.TryTake(booster)).IsTrue();

        var (target, last) = TargetOf(LastSeat);
        await AimAt(last.GlobalPosition, last);
        for (var i = 0; i < count; i++)
        {
            AssertBool(ReferenceEquals(_interaction.Target, target))
                .OverrideFailureMessage($"click {i + 1} aimed at {Describe(_interaction.Target)}, expected {target.Name}")
                .IsTrue();
            _leftClick!.Invoke(new MouseActionArgs(true, false));
            await BoundedWait.SyncPhysicsFrames(AimFrames);
        }

        AssertThat(target.Level!.Count).IsEqual(count);
        AssertThat(_hand.Count).IsEqual(0);
        return target.Level;
    }

    // Aims at `seat`, which holds stock, clicks the level's last booster into the hand, brings it to the front, and
    // opens it with the right button: the eight cards it holds go into the hand and the wrapper is gone. The hand holds
    // no cards before, only booster packs if any. A booster with a seed keeps it into the hand.
    private async Task TakeOffAndOpen(ShelfLevel level, DeckSlot seat, ulong? seed)
    {
        var target = _productShelf.LevelTargets.Single(t => t.Level == level);
        var before = level.Count;
        await AimAt(seat.GlobalPosition, seat);
        AssertBool(ReferenceEquals(_interaction.Target, target))
            .OverrideFailureMessage($"aimed at {Describe(_interaction.Target)}, expected {target.Name}")
            .IsTrue();
        AssertThat(_interaction.Prompt).IsEqual("Pick up booster pack");

        var heldBefore = _hand.Items.ToList();
        _leftClick!.Invoke(new MouseActionArgs(true, false));
        AssertThat(level.Count).IsEqual(before - 1);
        var booster = (CardContainer)_hand.Items.Single(item => !heldBefore.Contains(item));
        if (seed is { } expected)
            AssertThat(booster.ContentSeed).IsEqual(expected);
        AssertBool(_hand.SetFront(_hand.Items.ToList().IndexOf(booster))).IsTrue();
        await BoundedWait.SyncPhysicsFrames(AimFrames);

        using (FastClock.Start())
        {
            _rightPress!.Invoke(new MouseActionArgs(true, false));
            _rightPress.Invoke(new MouseActionArgs(false, false));
            AssertBool(_hand.IsOpening)
                .OverrideFailureMessage(
                    $"the booster did not start opening: opened={booster.IsOpened}, held={booster.IsHeld}, " +
                    $"layer={booster.CollisionLayer}, mask={booster.CollisionMask}, frozen={booster.Freeze}, " +
                    $"process={booster.ProcessMode}, seeded={booster.ContentSeed != null}")
                .IsTrue();
            await ContainerOpeningWait.Until(() => !_hand.IsOpening, "the booster opening in the hand");
        }

        AssertBool(_hand.IsOpening).IsFalse();
        AssertThat(_hand.Cards.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertBool(_hand.Items.Contains(booster)).IsFalse();
        AssertBool(GodotObject.IsInstanceValid(booster) && !booster.IsQueuedForDeletion()).IsFalse();
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

    // The click target of the product-shelf level that holds the named seat, and that seat.
    private (ShelfLevelTarget Target, DeckSlot Seat) TargetOf(string seatName)
    {
        var seat = _productShelf.Slots.Single(slot => slot.Name == seatName);
        return (_productShelf.LevelTargets.Single(target => target.Level == _productShelf.LevelOf(seat)), seat);
    }

    // Stands the player StandingDistance in front of the seat's shelf and turns the camera onto `point`, so the
    // interaction ray reaches it the way a player's aim does.
    private async Task AimAt(Vector3 point, DeckSlot seat)
    {
        var front = -_productShelf.GlobalBasis.Z;
        front = new Vector3(front.X, 0f, front.Z).Normalized();
        var standing = seat.GlobalPosition + front * StandingDistance;
        _player.GlobalPosition = new Vector3(standing.X, PlayerStandHeight, standing.Z);
        _player.Rotation = Vector3.Zero;
        _interaction.Camera!.LookAt(point);
        await BoundedWait.SyncPhysicsFrames(AimFrames);
    }

    private static string Describe(IInteractable? target) =>
        target is Node node ? node.Name.ToString() : "nothing";

}
