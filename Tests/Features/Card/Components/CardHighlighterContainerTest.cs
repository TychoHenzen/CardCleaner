using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
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
    private const string CardScene = "res://Scenes/Components/CardShader.tscn";
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
        _input.When(x => x.RegisterAction(Arg.Any<object>(), "card_drop_prepare", MouseButton.Right, Arg.Any<Action<bool>>()))
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
    public void ContainerClick_WhileCardsHeld_Ignored()
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

        // Once the box is dropped, the same card click is accepted, which proves the click is wired.
        _rightPress!.Invoke(true);
        card.Interact();
        AssertBool(_cardHolder.HasCards).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RightPress_WhileHoldingContainer_Drops()
    {
        Build();
        var box = MakeBox();
        box.Interact();
        AssertBool(_containerHolder.IsHolding).IsTrue();

        _rightPress!.Invoke(true);

        AssertBool(_containerHolder.IsHolding).IsFalse();
        AssertBool(box.GetParent() == _cardSpawner).IsTrue();
        AssertBool(_dropper.IsPreparingDrop).IsFalse();
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

    private CardController MakeCard()
    {
        var card = GD.Load<PackedScene>(CardScene).Instantiate<CardController>();
        _world.AddChild(card);
        return card;
    }

    private CardContainer MakeBox()
    {
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.Generator = new CardPackGenerator(RandomSources.Create(Seed));
        box.Spawning = _spawner;
        _world.AddChild(box);
        return box;
    }
}
