using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Building;
using CardCleaner.Scripts.Features.Shop.Shelves;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Hand.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardHighlighterContainerTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string StorageRackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string TrashCanScene = "res://Scenes/Shop/Items/TrashCan.tscn";
    private const ulong Seed = 777;

    private Node3D _world = null!;
    private Camera3D _anchor = null!;
    private Mocking.MockCardSpawner _cardSpawner = null!;
    private RecordingCardSpawner _spawner = null!;
    private IInputService _input = null!;
    private InteractionSystem _interaction = null!;
    private Scripts.Features.Hand.Components.Hand _hand = null!;
    private CardDropper _dropper = null!;
    private DropPreview _preview = null!;
    private Action<MouseActionArgs>? _rightPress;
    private CardHighlighter _router = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _world = Assertions.AddNode(new Node3D());

        _anchor = new Camera3D { Name = "HandAnchor" };
        _world.AddChild(_anchor);

        _cardSpawner = new Mocking.MockCardSpawner { Name = "MockSpawner" };
        _world.AddChild(_cardSpawner);
        TestServices.Container.RegisterSingleton<ICardSpawner>(_cardSpawner);

        _spawner = new RecordingCardSpawner();

        _input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(_input);
        _input.When(x => x.RegisterAction(
                Arg.Any<object>(), "card_drop_prepare", MouseButton.Right, Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _rightPress = ci.Arg<Action<MouseActionArgs>>());

        _interaction = TestServices.Inject(new InteractionSystem());
        _interaction.Camera = _anchor;
        _hand = TestServices.Inject(new Scripts.Features.Hand.Components.Hand { Name = "Hand" });
        _dropper = TestServices.Inject(new CardDropper());
        _preview = new DropPreview { Name = "Preview" };

        // Into the world now, not in Build: a node the setup leaves outside the tree counts as an orphan of the setup.
        _world.AddChild(_hand);
        _world.AddChild(_preview);
        _world.AddChild(_dropper);
        _world.AddChild(_interaction);
    }

    [AfterTest]
    public static void Cleanup()
    {
        TestServices.Reset();
    }

    [TestCase]
    public void ContainerClick_PicksUp()
    {
        Build();
        var box = MakeBox();

        Click(box);

        AssertThat(_hand.Front).IsSame(box);
        AssertBool(box.GetParent() == _anchor).IsTrue();
    }

    [TestCase]
    public void ContainerClick_WhileCardsHeld_IgnoresABox()
    {
        var card = MakeCard();
        Build();
        _hand.TryTake(card);
        var box = MakeBox();

        Click(box);

        AssertBool(box.GetParent() == _world).IsTrue();
        AssertThat(_hand.Items.ToArray()).IsEqual(new Node3D[] { card });
    }

    // A booster pack joins the cards in hand after them; the card in front stays the front.
    [TestCase]
    public void ContainerClick_WhileCardsHeld_TakesABooster()
    {
        var card = MakeCard();
        Build();
        _hand.TryTake(card);
        var booster = MakeBooster();

        Click(booster);

        AssertThat(_hand.Items.ToArray()).IsEqual(new Node3D[] { card, booster });
        AssertThat(_hand.Front).IsSame(card);
    }

    // While a booster in front opens into the hand, the cards in hand are not used: a card click, the right button, X
    // and a shelf click all do nothing until its cards have joined the hand.
    [TestCase]
    public void WhileABoosterOpensInTheHandTheCardsInHandAreNotUsed()
    {
        var held = MakeCard();
        var loose = MakeCard();
        Build();
        var target = FirstLevelTarget(AddProductShelf());
        _hand.TryTake(held);
        var booster = MakeBooster();
        Click(booster);
        _hand.Rotate(1);

        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));
        AssertBool(_hand.IsOpening).IsTrue();

        Click(loose);
        _rightPress.Invoke(new MouseActionArgs(true, false));
        _dropper.DropSingleCard();

        AssertThat(_hand.Cards.Count).IsEqual(1);
        AssertBool(_dropper.IsPreparingDrop).IsFalse();
        AssertObject(SpacePrompt(target)).IsNull();
        AssertBool(ClickSpace(target)).IsFalse();

        _rightPress.Invoke(new MouseActionArgs(false, false));
        booster.FinishOpening();
    }

    // A shelf level takes the hand's front item only: a card in front gives a product shelf nothing to put, and once
    // the wheel brings the booster pack to the front the level offers to take it.
    [TestCase]
    public void TheFrontItemIsWhatTheHandPutsOnAShelf()
    {
        var card = MakeCard();
        Build();
        var target = FirstLevelTarget(AddProductShelf());
        _hand.TryTake(card);
        Click(MakeBooster());

        AssertObject(SpacePrompt(target)).IsNull();

        _hand.Rotate(1);

        AssertThat(SpacePrompt(target)).IsEqual("Put the booster pack here");
    }

    // With a booster pack in front, the right button opens it, not a card drop.
    [TestCase]
    public void RightPress_WithABoosterPackInFront_OpensItInsteadOfAimingADrop()
    {
        var card = MakeCard();
        Build();
        _hand.TryTake(card);
        var booster = MakeBooster();
        Click(booster);
        _hand.Rotate(1);

        _rightPress!.Invoke(new MouseActionArgs(true, false));

        AssertBool(_dropper.IsPreparingDrop).IsFalse();
        AssertBool(booster.IsOpened).IsTrue();
        booster.FinishOpening();
    }

    [TestCase]
    public void CardClick_WhileContainerHeld_Ignored()
    {
        var card = MakeCard();
        Build();
        var box = MakeBox();
        Click(box);
        AssertThat(_hand.Count).IsEqual(1);

        // A card click while the box is held must not reach the hand.
        Click(card);
        AssertThat(_hand.Cards.Count).IsEqual(0);

        // Once the box is put down, the same card click is accepted, which proves the click is wired.
        _hand.PutFrontDown();
        Click(card);
        AssertThat(_hand.Cards.Count).IsEqual(1);
    }

    // A full hand offers no click on a loose card, so the aim prompt never promises a pick-up that does nothing.
    [TestCase]
    public void ACardIsNotOfferedToAFullHand()
    {
        var card = MakeCard();
        Build();
        for (var i = 0; i < IHand.MostPlaces; i++)
        {
            var held = ShopTestCards.Create(null);
            _world.AddChild(held);
            _hand.TryTake(held);
        }

        AssertObject(RouterClicks.Prompt(_router, card)).IsNull();
        Click(card);

        AssertThat(_hand.Cards.Count).IsEqual(IHand.MostPlaces);
        AssertBool(card.GetParent() == _world).IsTrue();
    }

    [TestCase]
    public void RightPress_WhileHoldingABox_OpensThenClosesItInTheHand()
    {
        Build();
        var box = MakeBox();
        Click(box);
        AssertThat(_hand.Count).IsEqual(1);

        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));

        AssertBool(box.IsLidOpen).IsTrue();
        AssertThat(_hand.Front).IsSame(box);
        AssertBool(_dropper.IsPreparingDrop).IsFalse();

        _rightPress.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));

        AssertBool(box.IsLidOpen).IsFalse();
        AssertThat(_hand.Front).IsSame(box);
    }

    // A shut box goes whole onto a rack, never onto a product shelf; an open one gives a product shelf one booster box
    // at a time.
    [TestCase]
    public void AShutBoxGoesOnARackWholeAndAnOpenBoxGivesAShelfOneBoosterBoxAtATime()
    {
        Build();
        var shelf = FirstLevelTarget(AddProductShelf());
        var rack = FirstLevelTarget(AddShelf(StorageRackScene, new Vector3(-5f, 0f, 0f)));
        var box = MakeBox();
        Click(box);

        AssertThat(SpacePrompt(rack)).IsEqual("Put the cardboard box here");
        AssertObject(SpacePrompt(shelf)).IsNull();

        _hand.UseFront();

        AssertThat(SpacePrompt(shelf)).IsEqual("Put the booster box here");
    }

    [TestCase]
    public void StockingFromAnOpenBoxPutsOneBoosterBoxInTheLevelsFirstSeatAndKeepsTheBoxInHand()
    {
        Build();
        var target = FirstLevelTarget(AddProductShelf());
        var box = MakeBox();
        Click(box);
        _hand.UseFront();
        var level = target.Level!;

        AssertBool(ClickSpace(target)).IsTrue();

        AssertThat(level.Seats[0].Cards.Count).IsEqual(1);
        AssertThat(((CardContainer)level.Seats[0].Cards[0]).Kind).IsEqual(CardContainerKind.Pack);
        AssertThat(_hand.Front).IsSame(box);
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 1);
        AssertThat(SpacePrompt(target)).IsEqual("Put the booster box here");
    }

    // A box moves on and off a rack level as a record: it comes back as a new body with what it held.
    [TestCase]
    public void ABoxPutOnARackComesBackOffItWithItsContentsAndSeed()
    {
        Build();
        var rack = FirstLevelTarget(AddShelf(StorageRackScene, new Vector3(-5f, 0f, 0f)));
        var box = MakeBox();
        var held = new StoredCard(null, 3, null);
        box.Storage!.Restore(new StoredItem[] { held });
        box.ContentSeed = 11UL;
        Click(box);

        AssertBool(ClickSpace(rack)).IsTrue();

        AssertThat(_hand.Count).IsEqual(0);
        AssertThat(rack.Level!.Stock.Count).IsEqual(1);
        var stocked = (CardContainer)rack.Level.Stock[0];
        // The box left the hand as a record and the level built it again, so the stocked body is a new one.
        AssertBool(ReferenceEquals(stocked, box)).IsFalse();
        AssertThat(stocked.Kind).IsEqual(CardContainerKind.Box);
        AssertThat(stocked.ContentSeed).IsEqual(11UL);
        AssertThat(stocked.Storage!.Items.ToArray()).ContainsExactly(held);
        AssertThat(RouterClicks.PromptAt(_router, rack, rack.Level.Seats[0].SeatPosition(0)))
            .IsEqual("Pick up cardboard box");

        AssertBool(RouterClicks.ClickAt(_router, rack, rack.Level.Seats[0].SeatPosition(0))).IsTrue();

        AssertThat(rack.Level.Stock.Count).IsEqual(0);
        var back = (CardContainer)_hand.Front!;
        AssertThat(back.ContentSeed).IsEqual(11UL);
        AssertThat(back.Storage!.Items.ToArray()).ContainsExactly(held);
    }

    [TestCase]
    public void AShutBoxIsNotStockedOnAProductShelf()
    {
        Build();
        var shelf = AddProductShelf();
        var box = MakeBox();
        Click(box);

        AssertBool(ClickSpace(FirstLevelTarget(shelf))).IsFalse();

        AssertThat(shelf.StockedCount).IsEqual(0);
        AssertThat(_hand.Front).IsSame(box);
    }

    // An emptied cardboard box stays in the hand, to be thrown away, and gives nothing more.
    [TestCase]
    public void TakingTheLastBoosterBoxLeavesTheEmptyBoxInHand()
    {
        Build();
        var shelf = AddProductShelf();
        var box = MakeBox();
        Click(box);
        _hand.UseFront();

        // Four booster boxes fill a level; the first two levels take the box's eight.
        foreach (var target in shelf.LevelTargets.Take(2))
        {
            for (var i = 0; i < target.Level!.Seats.Count; i++)
                AssertBool(ClickSpace(target)).IsTrue();
        }

        AssertThat(shelf.StockedCount).IsEqual(CardContainerLayout.ItemsPerContainer);
        AssertBool(box.IsOpened).IsFalse();
        AssertThat(box.Remaining).IsEqual(0);
        var free = shelf.LevelTargets.Last();
        AssertObject(SpacePrompt(free)).IsNull();
        AssertBool(ClickSpace(free)).IsFalse();
        AssertThat(_hand.Front).IsSame(box);
    }

    // The right button empties a held booster box into the hand; each shelf click then stocks the top booster pack,
    // four to a seat, filling the level's seats in order, and the rest stay in the hand.
    [TestCase]
    public async Task ABoosterBoxEmptiedIntoTheHandStocksOneBoosterPackPerClick()
    {
        Build();
        var target = FirstLevelTarget(AddProductShelf());
        Click(Make(PackScene));
        _rightPress!.Invoke(new MouseActionArgs(true, false));
        _rightPress.Invoke(new MouseActionArgs(false, false));
        AssertThat(_hand.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
        var level = target.Level!;

        for (var i = 0; i < ShelfPlacementRule.BoostersPerProductSlot; i++)
            AssertBool(ClickSpace(target)).IsTrue();

        AssertThat(level.Seats[0].Cards.Count).IsEqual(ShelfPlacementRule.BoostersPerProductSlot);
        AssertThat(_hand.Count)
            .IsEqual(CardContainerLayout.ItemsPerContainer - ShelfPlacementRule.BoostersPerProductSlot);
        AssertThat(SpacePrompt(target)).IsEqual("Put the booster pack here");

        AssertBool(ClickSpace(target)).IsTrue();
        AssertThat(level.Seats[1].Cards.Count).IsEqual(1);
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public void RightPress_WithCards_StillStartsCardDrop()
    {
        var card = MakeCard();
        Build();
        _hand.TryTake(card);

        _rightPress!.Invoke(new MouseActionArgs(true, false));

        AssertBool(_dropper.IsPreparingDrop).IsTrue();
    }

    // The router's one plan judges, prompts and clicks: a target shows a prompt exactly when a click on it moves
    // something, whatever the hand holds.
    [TestCase]
    public void APromptShowsExactlyWhenTheClickMovesSomething()
    {
        var loose = MakeCard();
        Build();
        var shelf = FirstLevelTarget(AddProductShelf());
        var bin = AddBin();

        AssertPromptMatchesClick(bin, bin.GlobalPosition, expectMove: false);
        AssertPromptMatchesClick(shelf, FreeSpot(shelf), expectMove: false);
        AssertPromptMatchesClick(loose, loose.GlobalPosition, expectMove: true);
        AssertPromptMatchesClick(shelf, FreeSpot(shelf), expectMove: false);

        var booster = MakeBooster();
        AssertPromptMatchesClick(booster, booster.GlobalPosition, expectMove: true);
        _hand.Rotate(1);
        AssertPromptMatchesClick(shelf, FreeSpot(shelf), expectMove: true);
    }

    // Task 7.5: Shift on an open cardboard box standing in the world takes as many items as the hand has room for.
    [TestCase]
    public async Task AShiftClickOnAnOpenBoxInTheWorldTakesAsManyBoosterPacksAsFit()
    {
        Build();
        var box = MakeBox();
        box.SetLidOpen(true, instantly: true);
        var storage = box.Storage!;
        while (storage.Count > 0)
            storage.TakeLast();
        var booster = new StoredContainer(BoosterScene, CardContainerKind.Booster, Seed, null);
        for (var i = 0; i < 5; i++)
            AssertBool(storage.Put(booster with { Seed = Seed + (ulong)i })).IsTrue();

        AssertThat(HandClickPlans.ShiftTakes(_hand, box, box.GlobalPosition)).IsEqual(5);
        AssertThat(RouterClicks.Prompt(_router, box, shift: true)).IsEqual("Take 5 booster packs from the box (5 in it)");
        AssertBool(Click(box, shift: true)).IsTrue();

        AssertThat(_hand.Count).IsEqual(5);
        AssertThat(storage.Count).IsEqual(0);
        AssertBool(box.IsHeld).IsFalse();
        await BoundedWait.ForQueuedFrees();
    }

    // Task 7.7: a Shift-click on an open box of 300 cards fills an empty hand with 100, and a hand with room for 30
    // takes 30; the rest stays in the box.
    [TestCase(0, 100)]
    [TestCase(70, 30)]
    public async Task AShiftClickOnAnOpenBoxOfCardsTakesAsManyAsTheHandHasRoomFor(int held, int taken)
    {
        // A card record becomes a body in the hand through the card spawning service.
        TestServices.Container.RegisterSingleton<ICardSpawningService>(new ShopTestCardSpawner());
        Build();
        for (var i = 0; i < held; i++)
            AssertBool(_hand.Put(new StoredCard(null, -1 - i, null))).IsTrue();
        var box = MakeBox();
        box.SetLidOpen(true, instantly: true);
        var storage = box.Storage!;
        while (storage.Count > 0)
            storage.TakeLast();
        for (var i = 0; i < 300; i++)
            AssertBool(storage.Put(new StoredCard(null, i, null))).IsTrue();

        AssertThat(RouterClicks.Prompt(_router, box, shift: true))
            .IsEqual($"Take {taken} cards from the box (300 in it)");
        AssertBool(Click(box, shift: true)).IsTrue();

        AssertThat(_hand.Count).IsEqual(held + taken);
        AssertThat(storage.Count).IsEqual(300 - taken);
        await BoundedWait.ForQueuedFrees();
    }

    // A booster box fills the hand, so a Shift take of booster boxes offers and takes one, and the hint for a Shift
    // take of many (which shows only above one) stays off.
    [TestCase]
    public void AShiftClickOnAnOpenBoxOfBoosterBoxesTakesOne()
    {
        Build();
        var box = MakeBox();
        box.SetLidOpen(true, instantly: true);

        AssertThat(HandClickPlans.ShiftTakes(_hand, box, box.GlobalPosition)).IsEqual(1);
        AssertThat(RouterClicks.Prompt(_router, box, shift: true)).IsEqual("Take 1 booster box from the box (8 in it)");
        AssertBool(Click(box, shift: true)).IsTrue();

        AssertThat(_hand.Count).IsEqual(1);
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 1);
    }

    // The bin takes an emptied cardboard box from the hand, and nothing else.
    [TestCase]
    public void TheBinTakesOnlyAnEmptiedBoxFromTheHand()
    {
        Build();
        var bin = AddBin();
        var box = MakeBox();
        AssertBool(Click(box)).IsTrue();

        AssertObject(RouterClicks.Prompt(_router, bin)).IsNull();

        var storage = box.Storage!;
        while (storage.Count > 0)
            storage.TakeLast();

        AssertThat(RouterClicks.Prompt(_router, bin)).IsEqual("Throw the box away");
        AssertBool(Click(bin)).IsTrue();
        AssertThat(_hand.Count).IsEqual(0);
    }

    // A rack level takes a booster box from the open cardboard box in front, one per click, and the box stays in hand.
    [TestCase]
    public void AClickOnARackLevelStocksOneBoosterBoxFromTheOpenBoxInHand()
    {
        Build();
        var rack = FirstLevelTarget(AddShelf(StorageRackScene, new Vector3(-5f, 0f, 0f)));
        var box = MakeBox();
        Click(box);
        _hand.UseFront();

        AssertThat(SpacePrompt(rack)).IsEqual("Put the booster box here");
        AssertBool(ClickSpace(rack)).IsTrue();

        AssertThat(rack.Level!.Count).IsEqual(1);
        AssertThat(((CardContainer)rack.Level.Last!).Kind).IsEqual(CardContainerKind.Pack);
        AssertThat(_hand.Front).IsSame(box);
        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 1);
    }

    // A click on nothing with an open box in front sets its last item down on the floor the player looks at.
    [TestCase]
    public async Task AClickOnTheFloorSetsTheOpenBoxsLastItemDown()
    {
        Build();
        AddFloor();
        _anchor.Rotation = new Vector3(-Mathf.Pi / 2f, 0f, 0f);
        var box = MakeBox();
        Click(box);
        _hand.UseFront();
        await ISceneRunner.SyncPhysicsFrame;

        var plan = _router.PlanForNothing();
        AssertThat(plan?.Prompt).IsEqual("Put the booster box on the floor");
        AssertBool(plan!.Apply()).IsTrue();

        AssertThat(box.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer - 1);
        AssertThat(_hand.Front).IsSame(box);
        var setDown = _world.FindChildren("*", "", true, false).OfType<CardContainer>()
            .Where(container => container != box && container.Kind == CardContainerKind.Pack).ToList();
        AssertThat(setDown.Count).IsEqual(1);
        AssertFloat(setDown[0].GlobalPosition.Y).IsBetween(-1.1f, 0f);
    }

    // A click on nothing sets an item from a held open box down; with a shut box it does nothing.
    [TestCase]
    public void AClickOnNothingOffersToSetAnItemDownOnlyFromAnOpenBox()
    {
        Build();
        var box = MakeBox();
        Click(box);

        AssertObject(_router.PlanForNothing()).IsNull();

        _hand.UseFront();

        AssertThat(_router.PlanForNothing()?.Prompt).IsEqual("Put the booster box on the floor");
    }

    // Nothing is clicked while a card drop is being aimed.
    [TestCase]
    public void NoClickIsPlannedWhileADropIsAimed()
    {
        var held = MakeCard();
        var loose = MakeCard();
        Build();
        _hand.TryTake(held);
        _rightPress!.Invoke(new MouseActionArgs(true, false));
        AssertBool(_dropper.IsPreparingDrop).IsTrue();

        AssertObject(RouterClicks.Prompt(_router, loose)).IsNull();
        AssertBool(Click(loose)).IsFalse();

        _rightPress.Invoke(new MouseActionArgs(false, false));
    }

    private void AssertPromptMatchesClick(IInteractable target, Vector3 point, bool expectMove)
    {
        var prompt = RouterClicks.PromptAt(_router, target, point);
        var moved = RouterClicks.ClickAt(_router, target, point);

        AssertBool(moved).OverrideFailureMessage($"click on {target.InteractionBody.Name} moved: {moved}")
            .IsEqual(expectMove);
        AssertBool(prompt != null).OverrideFailureMessage($"prompt '{prompt}' but moved: {moved}").IsEqual(moved);
    }

    // A floor 1 m under the hand anchor on the world layer, which a put-down aims at.
    private void AddFloor()
    {
        var floor = new StaticBody3D { Name = "Floor", CollisionLayer = 1 };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10f, 0.2f, 10f) } });
        _world.AddChild(floor);
        floor.Position = new Vector3(0f, -1.1f, 0f);
    }

    private TrashCan AddBin()
    {
        var bin = GD.Load<PackedScene>(TrashCanScene).Instantiate<TrashCan>();
        _world.AddChild(bin);
        bin.Position = new Vector3(0f, 0f, 5f);
        return bin;
    }

    private CardHighlighter Build()
    {
        var highlighter = TestServices.Inject(new CardHighlighter());
        highlighter.InteractionSystem = _interaction;
        highlighter.CardDropper = _dropper;
        highlighter.HandNode = _hand;
        highlighter.Preview = _preview;
        _world.AddChild(highlighter);
        _router = highlighter;
        return highlighter;
    }

    private bool Click(IInteractable target, bool shift = false) => RouterClicks.Click(_router, target, shift);

    // A board aimed at its first free seat (its last seat when every seat holds stock).
    private static Vector3 FreeSpot(ShelfLevelTarget target) =>
        (target.Level!.Seats.FirstOrDefault(seat => !seat.HasCards) ?? target.Level.Seats[^1]).SeatPosition(0);

    private string? SpacePrompt(ShelfLevelTarget target) => RouterClicks.PromptAt(_router, target, FreeSpot(target));

    private bool ClickSpace(ShelfLevelTarget target) => RouterClicks.ClickAt(_router, target, FreeSpot(target));

    private CardShelf AddProductShelf() => AddShelf(ProductShelfScene, new Vector3(5f, 0f, 0f));

    private static ShelfLevelTarget FirstLevelTarget(CardShelf shelf) => shelf.LevelTargets.First();

    private CardShelf AddShelf(string scene, Vector3 position)
    {
        var shelf = GD.Load<PackedScene>(scene).Instantiate<CardShelf>();
        _world.AddChild(shelf);
        shelf.Position = position;
        return shelf;
    }

    private CardController MakeCard()
    {
        var card = GD.Load<PackedScene>(CardScene).Instantiate<CardController>();
        _world.AddChild(card);
        return card;
    }

    private CardContainer MakeBox() => Make(BoxScene);

    private CardContainer MakeBooster() => Make(BoosterScene);

    private CardContainer Make(string scenePath)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.Generator = new CardPackGenerator(RandomSources.Create(Seed));
        container.Spawning = _spawner;
        _world.AddChild(container);
        return container;
    }
}
