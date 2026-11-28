using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Tests.TestUtilities;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardDropperTest
{
    private CardDropper _dropper = null!;
    private IHand _hand = null!;
    private IDropPreview _preview = null!;
    private IInputService _input = null!;
    private Action? _xPress;

    private int _started;
    private int _cancelled;
    private int _completed;

    [BeforeTest]
    public void Setup()
    {
        _hand = Substitute.For<IHand>();
        _hand.Cards.Returns(Array.Empty<RigidBody3D>());
        _preview = Substitute.For<IDropPreview>();
        _input = Substitute.For<IInputService>();
        _input.When(x => x.RegisterAction(Arg.Any<object>(), "card_drop_single", Key.X, Arg.Any<Action>()))
            .Do(ci => _xPress = ci.Arg<Action>());
        TestServices.Container.RegisterSingleton(_input);

        _dropper = TestServices.Inject(new CardDropper());
        Assertions.AddNode(_dropper);
        _dropper.Initialize(_hand, _preview);

        _started = _cancelled = _completed = 0;
        _dropper.DropStarted += () => _started++;
        _dropper.DropCancelled += () => _cancelled++;
        _dropper.DropCompleted += () => _completed++;
    }

    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    private RigidBody3D[] HoldCards(int count)
    {
        var cards = new RigidBody3D[count];
        for (var i = 0; i < count; i++)
            cards[i] = Assertions.AddNode(new CardController());
        _hand.Cards.Returns(cards);
        return cards;
    }

    [TestCase]
    public void InitialState_IsNotPreparingDrop()
    {
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
    }

    [TestCase]
    public void StartDropPreparation_WithCards_LaysThemOutAndShowsThePreview()
    {
        HoldCards(3);

        _dropper.StartDropPreparation();

        Assertions.AssertBool(_dropper.IsPreparingDrop).IsTrue();
        Assertions.AssertInt(_started).IsEqual(1);
        _preview.Received(1).ShowPreview(true);
        _hand.Received(1).PositionCardsForDrop();
    }

    [TestCase]
    public void StartDropPreparation_WithoutCards_DoesNotStart()
    {
        _dropper.StartDropPreparation();

        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
        Assertions.AssertInt(_started).IsEqual(0);
    }

    [TestCase]
    public void StartDropPreparation_WhileABoosterOpensInTheHand_DoesNotStart()
    {
        HoldCards(2);
        _hand.IsOpening.Returns(true);

        _dropper.StartDropPreparation();

        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
    }

    [TestCase]
    public void CancelDropPreparation_WhenPreparing_PutsTheHandBack()
    {
        HoldCards(3);
        _dropper.StartDropPreparation();

        _dropper.CancelDropPreparation();

        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
        Assertions.AssertInt(_cancelled).IsEqual(1);
        _hand.Received(1).PositionItems();
    }

    [TestCase]
    public void CancelDropPreparation_WhenNotPreparing_DoesNothing()
    {
        _dropper.CancelDropPreparation();

        Assertions.AssertInt(_cancelled).IsEqual(0);
    }

    // A completed drop lets go of every held card, the last one first; booster packs beside them stay in the hand.
    [TestCase]
    public void CompleteDropPreparation_ReleasesEveryCardLastFirst()
    {
        var cards = HoldCards(3);
        _dropper.StartDropPreparation();

        _dropper.CompleteDropPreparation();

        Received.InOrder(() =>
        {
            _hand.ReleaseCard(cards[2]);
            _hand.ReleaseCard(cards[1]);
            _hand.ReleaseCard(cards[0]);
        });
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
        Assertions.AssertInt(_completed).IsEqual(1);
    }

    [TestCase]
    public void CompleteDropPreparation_WhenNotPreparing_DoesNothing()
    {
        HoldCards(1);

        _dropper.CompleteDropPreparation();

        _hand.DidNotReceive().ReleaseCard(Arg.Any<RigidBody3D>());
        Assertions.AssertInt(_completed).IsEqual(0);
    }

    [TestCase]
    public void X_WithACardInFront_ReleasesThatCardOnly()
    {
        var cards = HoldCards(2);
        _hand.Front.Returns(cards[0]);

        _xPress!.Invoke();

        _hand.Received(1).ReleaseCard(cards[0]);
        _hand.DidNotReceive().ReleaseCard(cards[1]);
    }

    [TestCase]
    public void X_WithAContainerInFront_ReleasesNothing()
    {
        HoldCards(2);
        _hand.Front.Returns(Assertions.AddNode(new Node3D()));

        _dropper.DropSingleCard();

        _hand.DidNotReceive().ReleaseCard(Arg.Any<RigidBody3D>());
    }

    [TestCase]
    public void X_WhileADropIsAimed_ReleasesNothing()
    {
        var cards = HoldCards(2);
        _hand.Front.Returns(cards[0]);
        _dropper.StartDropPreparation();

        _dropper.DropSingleCard();

        _hand.DidNotReceive().ReleaseCard(Arg.Any<RigidBody3D>());
    }

    [TestCase]
    public void UpdateDropPreview_WhenNotPreparing_DoesNothing()
    {
        HoldCards(1);

        _dropper.UpdateDropPreview();

        _preview.DidNotReceive().UpdatePreview(Arg.Any<Vector3>(), Arg.Any<Vector3>());
    }
}
