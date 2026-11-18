using System;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardHighlighterContainerTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const ulong Seed = 777;

    private Node3D _world = null!;
    private Camera3D _hand = null!;
    private Mocking.MockCardSpawner _cardSpawner = null!;
    private RecordingCardSpawner _spawner = null!;
    private IInputService _input = null!;
    private InteractionSystem _interaction = null!;
    private CardHolder _cardHolder = null!;
    private CardDropper _dropper = null!;
    private DropPreview _preview = null!;
    private ContainerHolder _containerHolder = null!;
    private Action<bool>? _rightPress;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _world = Assertions.AddNode(new Node3D());

        _hand = new Camera3D { Name = "HandAnchor" };
        _world.AddChild(_hand);

        _cardSpawner = new Mocking.MockCardSpawner { Name = "MockSpawner" };
        _world.AddChild(_cardSpawner);
        TestServices.Container.RegisterSingleton<ICardSpawner>(_cardSpawner);

        _spawner = new RecordingCardSpawner();

        _input = Substitute.For<IInputService>();
        TestServices.Container.RegisterSingleton(_input);
        _input.When(x => x.RegisterAction(
                Arg.Any<object>(), "card_drop_prepare", MouseButton.Right, Arg.Any<Action<bool>>()))
            .Do(ci => _rightPress = ci.Arg<Action<bool>>());

        _interaction = TestServices.Inject(new InteractionSystem());
        _interaction.Camera = _hand;
        _cardHolder = new CardHolder { Name = "CardHolder" };
        _dropper = TestServices.Inject(new CardDropper());
        _preview = new DropPreview { Name = "Preview" };
        _containerHolder = TestServices.Inject(new ContainerHolder());
    }

    [AfterTest]
    public static void Cleanup()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ContainerClick_PicksUp()
    {
        Build();
        var box = MakeBox();

        box.Interact();

        AssertThat(_containerHolder.Held).IsSame(box);
        AssertBool(box.GetParent() == _hand).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ContainerClick_WhileCardsHeld_IgnoresABox()
    {
        var card = MakeCard();
        Build();
        _cardHolder.AddCard(card);
        var box = MakeBox();

        box.Interact();

        AssertBool(_containerHolder.IsHolding).IsFalse();
        AssertBool(box.GetParent() == _world).IsTrue();
        AssertBool(_cardHolder.HasCards).IsTrue();
    }

    // A booster opens into the cards in hand, so it is the one container picked up while cards are held.
    [TestCase]
    [TestCategory("Unit")]
    public void ContainerClick_WhileCardsHeld_TakesABooster()
    {
        var card = MakeCard();
        Build();
        _cardHolder.AddCard(card);
        var booster = MakeBooster();

        booster.Interact();

        AssertThat(_containerHolder.Held).IsSame(booster);
        AssertThat(_cardHolder.HeldCount).IsEqual(1);
    }

    // While a held booster opens into the hand, the cards in hand are not used: a card click, the right button, X and
    // a shelf click all do nothing until its cards have joined the hand.
    [TestCase]
    [TestCategory("Unit")]
    public void WhileABoosterOpensInTheHandTheCardsInHandAreNotUsed()
    {
        var held = MakeCard();
        var loose = MakeCard();
        var highlighter = Build();
        var shelf = AddProductShelf();
        _cardHolder.AddCard(held);
        var booster = MakeBooster();
        booster.Interact();

        _rightPress!.Invoke(true);
        _rightPress.Invoke(false);
        AssertBool(_containerHolder.IsOpening).IsTrue();

        loose.Interact();
        _rightPress.Invoke(true);
        _dropper.DropSingleCard();

        AssertThat(_cardHolder.HeldCount).IsEqual(1);
        AssertBool(_dropper.IsPreparingDrop).IsFalse();
        AssertObject(highlighter.Offered).IsNull();
        AssertBool(highlighter.TryStock(shelf.Slots.First())).IsFalse();

        _rightPress.Invoke(false);
        booster.FinishOpening();
    }

    // Holding a booster with cards in hand offers the booster to a shelf, not the top card.
    [TestCase]
    [TestCategory("Unit")]
    public void AHeldBoosterIsOfferedBeforeTheCardsInHand()
    {
        var card = MakeCard();
        var highlighter = Build();
        _cardHolder.AddCard(card);
        MakeBooster().Interact();

        AssertThat(highlighter.Offered).IsEqual(new StockOffer(StockKind.Booster));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardClick_WhileContainerHeld_Ignored()
    {
        var card = MakeCard();
        Build();
        var box = MakeBox();
        box.Interact();
        AssertBool(_containerHolder.IsHolding).IsTrue();

        // A card click while the box is held must not reach the hand.
        card.Interact();
        AssertBool(_cardHolder.HasCards).IsFalse();

        // Once the box is put down, the same card click is accepted, which proves the click is wired.
        _containerHolder.Drop();
        card.Interact();
        AssertBool(_cardHolder.HasCards).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RightPress_WhileHoldingABox_OpensThenClosesItInTheHand()
    {
        Build();
        var box = MakeBox();
        box.Interact();
        AssertBool(_containerHolder.IsHolding).IsTrue();

        _rightPress!.Invoke(true);
        _rightPress.Invoke(false);

        AssertBool(box.IsLidOpen).IsTrue();
        AssertThat(_containerHolder.Held).IsSame(box);
        AssertBool(_dropper.IsPreparingDrop).IsFalse();

        _rightPress.Invoke(true);
        _rightPress.Invoke(false);

        AssertBool(box.IsLidOpen).IsFalse();
        AssertThat(_containerHolder.Held).IsSame(box);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheHandOffersAShutBoxWholeAndAnOpenBoxOnePackAtATime()
    {
        var highlighter = Build();
        var box = MakeBox();
        box.Interact();

        AssertThat(highlighter.Offered).IsEqual(new StockOffer(StockKind.Box));

        _containerHolder.UseHeld();

        AssertThat(highlighter.Offered).IsEqual(new StockOffer(StockKind.Pack, 8));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StockingFromAnOpenBoxPutsOnePackInTheSlotAndKeepsTheBoxInHand()
    {
        var highlighter = Build();
        var shelf = AddProductShelf();
        var box = MakeBox();
        box.Interact();
        _containerHolder.UseHeld();
        var slot = shelf.Slots.First();

        AssertBool(highlighter.TryStock(slot)).IsTrue();

        AssertThat(slot.Cards.Count).IsEqual(1);
        AssertThat(((CardContainer)slot.Cards[0]).Kind).IsEqual(CardContainerKind.Pack);
        AssertThat(_containerHolder.Held).IsSame(box);
        AssertThat(highlighter.Offered).IsEqual(new StockOffer(StockKind.Pack, 7));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AShutBoxIsNotStockedOnAProductShelf()
    {
        var highlighter = Build();
        var shelf = AddProductShelf();
        var box = MakeBox();
        box.Interact();

        AssertBool(highlighter.TryStock(shelf.Slots.First())).IsFalse();

        AssertThat(shelf.StockedCount).IsEqual(0);
        AssertThat(_containerHolder.Held).IsSame(box);
    }

    // An emptied box is freed only at the end of the frame, so within that frame its count must stop a ninth take.
    [TestCase]
    [TestCategory("Unit")]
    public void TheLastPackTakenEmptiesTheHandInTheSameFrame()
    {
        var highlighter = Build();
        var shelf = AddProductShelf();
        var box = MakeBox();
        box.Interact();
        _containerHolder.UseHeld();

        foreach (var slot in shelf.Slots)
            AssertBool(highlighter.TryStock(slot)).IsTrue();

        AssertThat(shelf.StockedCount).IsEqual(8);
        AssertBool(box.IsOpened).IsTrue();
        AssertObject(highlighter.Offered).IsNull();
        AssertBool(_containerHolder.IsHolding).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RightPress_WithCards_StillStartsCardDrop()
    {
        var card = MakeCard();
        Build();
        _cardHolder.AddCard(card);

        _rightPress!.Invoke(true);

        AssertBool(_dropper.IsPreparingDrop).IsTrue();
    }

    private CardHighlighter Build()
    {
        _world.AddChild(_cardHolder);
        _world.AddChild(_preview);
        var highlighter = TestServices.Inject(new CardHighlighter());
        highlighter.InteractionSystem = _interaction;
        highlighter.CardDropper = _dropper;
        highlighter.CardHolder = _cardHolder;
        highlighter.Preview = _preview;
        highlighter.ContainerHolderNode = _containerHolder;
        _world.AddChild(_dropper);
        _world.AddChild(_containerHolder);
        _world.AddChild(_interaction);
        _world.AddChild(highlighter);
        return highlighter;
    }

    private CardShelf AddProductShelf()
    {
        var shelf = GD.Load<PackedScene>(ProductShelfScene).Instantiate<CardShelf>();
        _world.AddChild(shelf);
        shelf.Position = new Vector3(5f, 0f, 0f);
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
