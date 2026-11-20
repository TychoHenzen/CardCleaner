using System;
using System.Collections.Generic;
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
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Hand.Components;

/// <summary>
///     The player's one hand on live bodies: what it takes and refuses, how a held body is put out of play and back,
///     the order and the front item (wheel, Q, X's release), the fan with the front raised, the carried box, and the
///     room a booster in front needs for its cards.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class HandTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong Seed = 777;

    private Node3D _world = null!;
    private Camera3D _anchor = null!;
    private Mocking.MockCardSpawner _cardSpawner = null!;
    private RecordingCardSpawner _spawner = null!;
    private Scripts.Features.Hand.Components.Hand _hand = null!;
    private Action? _qPress;
    private Action<MouseActionArgs>? _wheelUp;
    private Action<MouseActionArgs>? _wheelDown;

    [Before]
    public static void SetupSuite()
    {
        TestServices.Reset();
    }

    [BeforeTest]
    public void Setup()
    {
        _world = Assertions.AddNode(new Node3D());
        // The hand anchor is the player's camera in the game, so a held body's parent is a Camera3D.
        _anchor = new Camera3D { Name = "HandAnchor" };
        _world.AddChild(_anchor);

        _cardSpawner = new Mocking.MockCardSpawner { Name = "MockSpawner" };
        _world.AddChild(_cardSpawner);
        TestServices.Container.RegisterSingleton<ICardSpawner>(_cardSpawner);
        _spawner = new RecordingCardSpawner();

        var input = Substitute.For<IInputService>();
        input.When(x => x.RegisterAction(Arg.Any<object>(), "hand_put_down", Key.Q, Arg.Any<Action>()))
            .Do(ci => _qPress = ci.Arg<Action>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "hand_next", MouseButton.WheelUp,
                Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _wheelUp = ci.Arg<Action<MouseActionArgs>>());
        input.When(x => x.RegisterAction(Arg.Any<object>(), "hand_previous", MouseButton.WheelDown,
                Arg.Any<Action<MouseActionArgs>>()))
            .Do(ci => _wheelDown = ci.Arg<Action<MouseActionArgs>>());
        TestServices.Container.RegisterSingleton(input);

        _hand = TestServices.Inject(new Scripts.Features.Hand.Components.Hand());
        _world.AddChild(_hand);
        _hand.SetReferences(_anchor);
    }

    [AfterTest]
    public static void Cleanup()
    {
        TestServices.Reset();
    }

    [TestCase]
    public void AnEmptyHandHasNoFrontAndAllItsRoom()
    {
        AssertThat(_hand.Count).IsEqual(0);
        AssertThat(_hand.FrontIndex).IsEqual(-1);
        AssertThat(_hand.Front).IsNull();
        AssertThat(_hand.Room).IsEqual(IHand.MostPlaces);
    }

    [TestCase]
    public void ATakenCardIsFrozenOffEveryLayerWithItsShapeOffUnderTheAnchor()
    {
        var card = MakeCard("Card");

        AssertBool(_hand.TryTake(card)).IsTrue();

        AssertBool(card.Freeze).IsTrue();
        AssertThat(card.CollisionLayer).IsEqual(0u);
        AssertBool(card.GetNode<CollisionShape3D>("CardCollision").Disabled).IsTrue();
        AssertBool(card.GetParent() == _anchor).IsTrue();
        AssertThat(_hand.Cards.ToArray()).IsEqual(new RigidBody3D[] { card });
    }

    [TestCase]
    public void ATakenContainerIsFrozenOnNoLayerOrMaskUnderTheAnchor()
    {
        var booster = Make(BoosterScene);
        booster.CollisionLayer = 4;
        booster.CollisionMask = 6;

        AssertBool(_hand.TryTake(booster)).IsTrue();

        AssertBool(booster.IsHeld).IsTrue();
        AssertBool(booster.Freeze).IsTrue();
        AssertThat(booster.CollisionLayer).IsEqual(0u);
        AssertThat(booster.CollisionMask).IsEqual(0u);
    }

    [TestCase]
    public void TheSameBodyIsNotTakenTwice()
    {
        var card = MakeCard("Card");
        _hand.TryTake(card);

        AssertBool(_hand.TryTake(card)).IsFalse();

        AssertThat(_hand.Count).IsEqual(1);
    }

    [TestCase]
    public void AFreedBodyOrABodyThatIsNeitherCardNorContainerIsNotTaken()
    {
        var gone = MakeCard("Gone");
        gone.Free();
        var other = new RigidBody3D();
        _world.AddChild(other);

        AssertBool(_hand.TryTake(gone)).IsFalse();
        AssertBool(_hand.TryTake(other)).IsFalse();
        AssertThat(_hand.Count).IsEqual(0);
    }

    // Cards and booster packs share the hand in the order they were taken, and the first one stays the front.
    [TestCase]
    public void CardsAndBoosterPacksKeepTheOrderTheyWereTakenIn()
    {
        var first = MakeCard("First");
        var booster = Make(BoosterScene);
        var last = MakeCard("Last");

        _hand.TryTake(first);
        _hand.TryTake(booster);
        _hand.TryTake(last);

        AssertThat(_hand.Items.ToArray()).IsEqual(new Node3D[] { first, booster, last });
        AssertThat(_hand.Cards.ToArray()).IsEqual(new RigidBody3D[] { first, last });
        AssertThat(_hand.Front).IsSame(first);
    }

    [TestCase]
    public void ABoxOrBoosterBoxIsTakenOnlyIntoAnEmptyHandAndThenTakesNothingElse()
    {
        var card = MakeCard("Card");
        var box = Make(BoxScene);
        _hand.TryTake(card);

        AssertBool(_hand.TryTake(box)).IsFalse();
        AssertBool(_hand.TryTake(Make(PackScene))).IsFalse();

        _hand.PutFrontDown();
        AssertBool(_hand.TryTake(box)).IsTrue();
        AssertBool(_hand.TryTake(card)).IsFalse();
        AssertBool(_hand.TryTake(Make(BoosterScene))).IsFalse();
        AssertThat(_hand.Room).IsEqual(0);
        AssertThat(_hand.Front).IsSame(box);
    }

    [TestCase]
    public void TheHandHoldsAtMostItsPlacesAndRefusesOneMore()
    {
        var cards = Enumerable.Range(0, IHand.MostPlaces).Select(i => MakeCard($"Card{i}")).ToList();
        foreach (var card in cards)
            AssertBool(_hand.TryTake(card)).IsTrue();

        AssertThat(_hand.Room).IsEqual(0);
        AssertBool(_hand.TryTake(Make(BoosterScene))).IsFalse();
        AssertThat(_hand.Count).IsEqual(IHand.MostPlaces);
    }

    [TestCase]
    public void TheWheelMovesTheFrontOnThePressOnlyAndWrapsAtEitherEnd()
    {
        var items = new Node3D[] { MakeCard("A"), Make(BoosterScene), MakeCard("C") };
        foreach (var item in items)
            _hand.TryTake(item);

        _wheelUp!.Invoke(new MouseActionArgs(true, false));
        AssertThat(_hand.Front).IsSame(items[1]);
        _wheelUp!.Invoke(new MouseActionArgs(false, false));
        AssertThat(_hand.Front).IsSame(items[1]);
        _wheelUp!.Invoke(new MouseActionArgs(true, false));
        _wheelUp!.Invoke(new MouseActionArgs(true, false));
        AssertThat(_hand.Front).IsSame(items[0]);
        _wheelDown!.Invoke(new MouseActionArgs(true, false));
        AssertThat(_hand.Front).IsSame(items[2]);
    }

    [TestCase]
    public void SetFrontMakesThatItemTheFrontAndRefusesAMissingOne()
    {
        var items = new Node3D[] { MakeCard("A"), MakeCard("B"), MakeCard("C") };
        foreach (var item in items)
            _hand.TryTake(item);

        AssertBool(_hand.SetFront(2)).IsTrue();
        AssertThat(_hand.Front).IsSame(items[2]);
        AssertBool(_hand.SetFront(3)).IsFalse();
        AssertThat(_hand.Front).IsSame(items[2]);
    }

    // The fan runs left to right in hand order with every face to the camera; the front item stands out of it along its
    // own lean and a little nearer the camera, and moves out again when the front moves on.
    [TestCase]
    public void TheFrontItemIsRaisedOutOfTheFan()
    {
        var cards = new[] { MakeCard("A"), MakeCard("B"), MakeCard("C") };
        foreach (var card in cards)
            _hand.TryTake(card);
        var fan = new CardFan(_hand.FanCenter, _hand.FanRadius, _hand.FanSpreadDegrees, _hand.FanFillCount,
            _hand.FanDepthStep);

        AssertThat(cards[0].Position.X).IsLess(cards[1].Position.X);
        AssertThat(cards[1].Position.X).IsLess(cards[2].Position.X);
        AssertBool(cards[1].Transform.IsEqualApprox(fan.Pose(1, 3))).IsTrue();
        AssertBool(cards[0].Transform.IsEqualApprox(fan.Pose(0, 3))).IsFalse();
        AssertThat(cards[0].Position.DistanceTo(fan.Pose(0, 3).Origin)).IsGreater(_hand.FrontRaise * 0.9f);

        _hand.Rotate(1);

        AssertBool(cards[0].Transform.IsEqualApprox(fan.Pose(0, 3))).IsTrue();
        AssertBool(cards[1].Transform.IsEqualApprox(fan.Pose(1, 3))).IsFalse();
    }

    [TestCase]
    public void ABoxIsCarriedLevelAtTheBoxOffset()
    {
        var box = Make(BoxScene);

        _hand.TryTake(box);

        AssertThat(box.Position).IsEqual(_hand.BoxHoldOffset);
        AssertBool(box.Basis.IsEqualApprox(Basis.Identity)).IsTrue();
    }

    [TestCase]
    public void QSetsTheFrontItemDownInFrontWithItsOwnCollisionAndTheNextBecomesTheFront()
    {
        var first = Make(BoosterScene);
        var second = Make(BoosterScene);
        first.CollisionLayer = 4;
        first.CollisionMask = 6;
        _hand.TryTake(first);
        _hand.TryTake(second);

        _qPress!.Invoke();

        AssertBool(first.GetParent() == _cardSpawner).IsTrue();
        AssertThat(first.GlobalPosition).IsEqual(new Vector3(0f, 0f, -_hand.PutDownDistance));
        AssertThat(first.CollisionLayer).IsEqual(4u);
        AssertThat(first.CollisionMask).IsEqual(6u);
        AssertBool(first.Freeze).IsFalse();
        AssertThat(_hand.Front).IsSame(second);
        AssertBool(second.GetParent() == _anchor).IsTrue();
    }

    [TestCase]
    public void QSetsAFrontCardDownWithItsShapeAndLayerBack()
    {
        var card = MakeCard("Card");
        _hand.TryTake(card);

        _qPress!.Invoke();

        AssertBool(card.GetParent() == _cardSpawner).IsTrue();
        AssertThat(card.CollisionLayer).IsEqual(_hand.CardCollisionLayer);
        AssertBool(card.GetNode<CollisionShape3D>("CardCollision").Disabled).IsFalse();
        AssertThat(_hand.Count).IsEqual(0);
    }

    [TestCase]
    public async Task ReleaseCardLetsTheCardFallAndTheRestStayFanned()
    {
        var cards = new[] { MakeCard("A"), MakeCard("B"), MakeCard("C") };
        foreach (var card in cards)
            _hand.TryTake(card);

        AssertBool(_hand.ReleaseCard(cards[0])).IsTrue();

        AssertBool(cards[0].GetParent() == _cardSpawner).IsTrue();
        AssertThat(_hand.Items.ToArray()).IsEqual(new Node3D[] { cards[1], cards[2] });
        AssertThat(_hand.Front).IsSame(cards[1]);
        AssertBool(cards[2].Transform.IsEqualApprox(_hand.PoseOf(1, 2))).IsTrue();
        await _world.ToSignal(_world.GetTree(), SceneTree.SignalName.ProcessFrame);
        AssertBool(cards[0].Freeze).IsFalse();
        AssertThat(cards[0].CollisionLayer).IsEqual(_hand.CardCollisionLayer);
    }

    [TestCase]
    public void ReleaseCardRefusesAContainerAndABodyTheHandDoesNotHold()
    {
        var booster = Make(BoosterScene);
        _hand.TryTake(booster);

        AssertBool(_hand.ReleaseCard(booster)).IsFalse();
        AssertBool(_hand.ReleaseCard(MakeCard("Loose"))).IsFalse();
        AssertThat(_hand.Count).IsEqual(1);
    }

    // A drop aimed with the right button lays the cards flat in front of the anchor; booster packs stay where they are.
    [TestCase]
    public void TheDropLayoutLaysOnlyTheCardsInFrontOfTheAnchor()
    {
        _anchor.GlobalPosition = new Vector3(0f, 5f, 0f);
        var card = MakeCard("Card");
        var booster = Make(BoosterScene);
        _hand.TryTake(card);
        _hand.TryTake(booster);
        var boosterPose = booster.Transform;

        _hand.PositionCardsForDrop();

        AssertThat(card.GlobalPosition.Y).IsEqualApprox(5f, 1e-4f);
        AssertThat(card.GlobalPosition.Z).IsEqualApprox(-_hand.HoldDistance, 1e-4f);
        AssertBool(booster.Transform.IsEqualApprox(boosterPose)).IsTrue();
    }

    [TestCase]
    public void HandOverMovesTheBodyIntoTheWorldWhereItHangsWithItsCollisionBack()
    {
        var booster = Make(BoosterScene);
        booster.CollisionLayer = 4;
        _hand.TryTake(booster);
        var hung = booster.GlobalPosition;

        AssertBool(_hand.HandOver(booster)).IsTrue();

        // Out of the camera before its collision is back, so it never re-enters the space inside the player.
        AssertBool(booster.GetParent() == _cardSpawner.GetNode()).IsTrue();
        AssertBool(booster.GlobalPosition.IsEqualApprox(hung)).IsTrue();
        AssertBool(booster.Freeze).IsTrue();
        AssertThat(booster.CollisionLayer).IsEqual(4u);
        AssertThat(_hand.Count).IsEqual(0);
    }

    [TestCase]
    public void AHeldBodyFreedFromTheMiddleLeavesTheHandAndTheRestCloseUp()
    {
        var items = new Node3D[] { MakeCard("A"), MakeCard("B"), MakeCard("C") };
        foreach (var item in items)
            _hand.TryTake(item);
        _hand.SetFront(2);

        items[1].Free();

        AssertThat(_hand.Count).IsEqual(2);
        AssertThat(_hand.Front).IsSame(items[2]);
        AssertBool(items[2].Transform.IsEqualApprox(_hand.PoseOf(1, 2))).IsTrue();
    }

    [TestCase]
    public void DiscardFrontFreesTheFrontItem()
    {
        var box = Make(BoxScene);
        _hand.TryTake(box);

        AssertBool(_hand.DiscardFront()).IsTrue();

        AssertThat(_hand.Count).IsEqual(0);
        AssertBool(box.IsQueuedForDeletion()).IsTrue();
    }

    [TestCase]
    public void UseFrontSwingsAHeldBoxOpenAndShut()
    {
        var box = Make(BoxScene);
        _hand.TryTake(box);

        AssertBool(_hand.UseFront()).IsTrue();
        AssertBool(box.IsLidOpen).IsTrue();
        AssertBool(_hand.UseFront()).IsTrue();
        AssertBool(box.IsLidOpen).IsFalse();
        AssertThat(_hand.Front).IsSame(box);
    }

    [TestCase]
    public void UseFrontOnACardDoesNothing()
    {
        _hand.TryTake(MakeCard("Card"));

        AssertBool(_hand.UseFront()).IsFalse();
    }

    // A booster box opened in the hand empties into it as its eight booster packs, the n-th with child seed n.
    [TestCase]
    public async Task UseFrontEmptiesABoosterBoxIntoTheHandAsItsEightBoosterPacks()
    {
        var pack = Make(PackScene);
        pack.ContentSeed = Seed;
        _hand.TryTake(pack);

        AssertBool(_hand.UseFront()).IsTrue();

        var held = _hand.Items.OfType<CardContainer>().ToList();
        AssertThat(held.Select(booster => booster.ContentSeed!.Value).ToArray()).IsEqual(Enumerable
            .Range(0, CardContainerLayout.ItemsPerContainer).Select(i => ContainerSeeds.ChildSeed(Seed, i)).ToArray());
        AssertBool(held.All(booster => booster is { Kind: CardContainerKind.Booster, Freeze: true, CollisionLayer: 0 }))
            .IsTrue();
        AssertBool(pack.IsInsideTree()).IsFalse();
        await BoundedWait.ForQueuedFrees();
    }

    // A booster in front opens only while its eight cards fit once it has left the hand.
    [TestCase]
    public void ABoosterInFrontFitsOnlyWhileTheHandHasRoomForItsCardsOnceItLeaves()
    {
        var booster = Make(BoosterScene);
        _hand.TryTake(booster);
        var fits = IHand.MostPlaces - CardContainerLayout.ItemsPerContainer;
        for (var i = 0; i < fits; i++)
            _hand.TryTake(MakeCard($"Card{i}"));
        AssertBool(_hand.FrontBoosterFits).IsTrue();

        _hand.TryTake(MakeCard("OneTooMany"));

        AssertBool(_hand.FrontBoosterFits).IsFalse();
        AssertBool(_hand.UseFront()).IsFalse();
        AssertBool(booster.IsOpened).IsFalse();
        AssertThat(_spawner.Spawned.Count).IsEqual(0);
    }

    [TestCase]
    public async Task TheHandJoinsTheHandsGroupWhileInTheTree()
    {
        var tree = _world.GetTree();
        AssertBool(_hand.IsInGroup(IHand.GroupName)).IsTrue();

        _hand.QueueFree();
        await _world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        AssertThat(tree.GetNodesInGroup(IHand.GroupName).Count).IsEqual(0);
    }

    [TestCase]
    public void PeekNamesTheFrontItemWithoutDrawingItsSeed()
    {
        var booster = Make(BoosterScene);
        booster.ShopPrice = 9;
        _hand.TryTake(booster);

        AssertThat(_hand.Peek()).IsEqual(new StoredContainer(BoosterScene, CardContainerKind.Booster, null, 9));
        AssertThat(booster.ContentSeed).IsNull();
    }

    [TestCase]
    public void TakeLastGivesTheFrontAsItsRecordAndFreesItsBody()
    {
        var card = MakeCard("Card");
        var booster = Make(BoosterScene);
        booster.ContentSeed = 31;
        booster.ShopPrice = 4;
        _hand.TryTake(card);
        _hand.TryTake(booster);
        _hand.SetFront(1);

        var taken = _hand.TakeLast();

        AssertThat(taken).IsEqual(new StoredContainer(BoosterScene, CardContainerKind.Booster, 31, 4));
        AssertBool(booster.IsQueuedForDeletion()).IsTrue();
        AssertThat(booster.GetParent()).IsNull();
        AssertThat(_hand.Items.ToArray()).IsEqual(new Node3D[] { card });
    }

    [TestCase]
    public void PutBuildsAContainerFromItsRecordAndTakesItIn()
    {
        AssertBool(_hand.Put(new StoredContainer(BoosterScene, CardContainerKind.Booster, 42, 9))).IsTrue();

        var booster = _hand.Front as CardContainer;
        AssertThat(booster).IsNotNull();
        AssertThat(booster!.ContentSeed).IsEqual(42ul);
        AssertThat(booster.ShopPrice).IsEqual(9);
        AssertBool(booster.GetParent() == _anchor).IsTrue();
        AssertBool(booster.Freeze).IsTrue();
        AssertThat(booster.CollisionLayer).IsEqual(0u);
    }

    [TestCase]
    public void PutBuildsACardFromItsRecordAndTakesItIn()
    {
        TestServices.Container.RegisterSingleton<ICardSpawningService>(new RecordingCardSpawner(buildBodies: true));

        AssertBool(_hand.Put(new StoredCard(null, 3, 12))).IsTrue();

        var card = _hand.Front as CardController;
        AssertThat(card).IsNotNull();
        AssertThat(card!.ShopPrice).IsEqual(12);
        AssertBool(card.HasSignature).IsFalse();
        AssertBool(card.GetParent() == _anchor).IsTrue();
        AssertThat(card.CollisionLayer).IsEqual(0u);
    }

    [TestCase]
    public void ABoosterBoxRecordGoesOnlyIntoAnEmptyHand()
    {
        var boosterBox = new StoredContainer(PackScene, CardContainerKind.Pack, 5, null);
        AssertBool(_hand.CanPut(boosterBox)).IsTrue();

        _hand.TryTake(MakeCard("Card"));

        AssertBool(_hand.CanPut(boosterBox)).IsFalse();
        AssertBool(_hand.Put(boosterBox)).IsFalse();
        AssertThat(_hand.Count).IsEqual(1);
    }

    [TestCase]
    public void WhileACardboardBoxIsHeldTheHandPeeksNothingTakesNothingAndTakesNoPut()
    {
        var box = Make(BoxScene);
        _hand.TryTake(box);

        AssertThat(_hand.Peek()).IsNull();
        AssertThat(_hand.TakeLast()).IsNull();
        AssertBool(_hand.CanPut(new StoredCard(null, 1, null))).IsFalse();
        AssertBool(_hand.Front == box).IsTrue();
    }

    private CardController MakeCard(string name)
    {
        var card = new CardController { Name = name };
        card.AddChild(new CollisionShape3D { Name = "CardCollision", Shape = new BoxShape3D() });
        card.AddChild(new CardDesigner { Name = "Designer" });
        _world.AddChild(card);
        return card;
    }

    private CardContainer Make(string scenePath)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.Generator = new CardPackGenerator(RandomSources.Create(Seed));
        container.Spawning = _spawner;
        _world.AddChild(container);
        return container;
    }
}
