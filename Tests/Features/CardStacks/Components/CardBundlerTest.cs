using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.CardStacks.Components;

/// <summary>
///     The hand's side of decks and piles: V straps the cards in hand into one deck that keeps every card; through the
///     hand router a click on a deck unstraps it into the hand when it fits, a click on a pile takes one card, and a
///     Shift-click on a pile takes as many as fit.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardBundlerTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";

    private Node3D _world = null!;
    private Camera3D _eye = null!;
    private Mocking.MockCardSpawner _spawnerNode = null!;
    private ShopTestCardSpawner _spawning = null!;
    private Scripts.Features.Hand.Components.Hand _hand = null!;
    private CardBundler _bundler = null!;
    private CardHighlighter _router = null!;
    private Action? _strapKey;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _world = AddNode(new Node3D());
        _eye = new Camera3D { Name = "Eye" };
        _world.AddChild(_eye);

        _spawnerNode = new Mocking.MockCardSpawner { Name = "Spawner" };
        _world.AddChild(_spawnerNode);
        TestServices.Container.RegisterSingleton<ICardSpawner>(_spawnerNode);
        _spawning = new ShopTestCardSpawner();
        TestServices.Container.RegisterSingleton<ICardSpawningService>(_spawning);

        var input = Substitute.For<IInputService>();
        input.When(x => x.RegisterAction(Arg.Any<object>(), "card_strap", Key.V, Arg.Any<Action>()))
            .Do(call => _strapKey = call.Arg<Action>());
        TestServices.Container.RegisterSingleton(input);

        var interaction = TestServices.Inject(new InteractionSystem { Camera = _eye });
        _hand = TestServices.Inject(new Scripts.Features.Hand.Components.Hand { Name = "Hand" });
        var dropper = TestServices.Inject(new CardDropper());
        // In the world, so it is freed with it; a node left outside the tree outlives the test.
        _world.AddChild(interaction);
        _world.AddChild(_hand);
        _world.AddChild(dropper);
        _hand.SetReferences(_eye);

        _bundler = TestServices.Inject(new CardBundler
        {
            InteractionSystem = interaction,
            HandNode = _hand,
            CardDropper = dropper,
        });
        _world.AddChild(_bundler);

        _router = TestServices.Inject(new CardHighlighter
        {
            InteractionSystem = interaction,
            HandNode = _hand,
            CardDropper = dropper,
            Preview = new DropPreview { Name = "Preview" },
        });
        _world.AddChild(_router.Preview);
        _world.AddChild(_router);
    }

    [AfterTest]
    public static void Cleanup() => TestServices.Reset();

    [TestCase]
    public async Task TheStrapKeyStrapsEveryCardInHandIntoOneDeckInFrontOfThePlayer()
    {
        var cards = HoldCards(3);

        _strapKey!.Invoke();

        AssertThat(_hand.Count).IsEqual(0);
        var deck = Bundles().Single();
        AssertThat(deck.Kind).IsEqual(CardBundleKind.Deck);
        AssertObject(deck.GetParent()).IsSame(_spawnerNode);
        AssertThat(deck.GlobalPosition.Z).IsLess(-0.5f);
        AssertThat(deck.Cards.Select(card => card.Price).ToArray()).IsEqual(new int?[] { 10, 11, 12 });
        AssertThat(deck.Cards.Select(card => card.LookSeed).ToArray()).IsEqual(new int?[] { 100, 101, 102 });
        AssertThat(deck.Cards.Select(card => card.Signature!.Value.Solidum).ToArray())
            .IsEqual(new[] { 0.1f, 0.2f, 0.3f });
        AssertBool(cards.All(card => !IsInstanceValid(card) || card.IsQueuedForDeletion())).IsTrue();
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public async Task ACardWithoutASignatureStaysWithoutOneThroughADeck()
    {
        var card = ShopTestCards.Create(null);
        _world.AddChild(card);
        _hand.TryTake(card);
        HoldCards(1);

        RouterClicks.Click(_router, _bundler.Strap()!);

        AssertBool(_hand.Cards.OfType<CardController>().First().HasSignature).IsFalse();
        AssertBool(_hand.Cards.OfType<CardController>().Last().HasSignature).IsTrue();
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public void OneCardIsNotStrapped()
    {
        HoldCards(1);

        AssertBool(_bundler.CanStrap).IsFalse();
        AssertObject(_bundler.Strap()).IsNull();
        AssertThat(_hand.Count).IsEqual(1);
    }

    // Only the cards go into the deck; a booster pack held among them stays in the hand.
    [TestCase]
    public async Task StrappingLeavesTheBoosterPacksInTheHand()
    {
        HoldCards(1);
        var booster = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        _world.AddChild(booster);
        AssertBool(_hand.TryTake(booster)).IsTrue();
        HoldCards(1);

        _strapKey!.Invoke();

        AssertThat(_hand.Items.ToArray()).IsEqual(new Node3D[] { booster });
        AssertThat(Bundles().Single().Count).IsEqual(2);
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public async Task ClickingADeckUnstrapsItIntoTheHandInTheOrderItWasStrapped()
    {
        HoldCards(3);
        var deck = _bundler.Strap()!;

        AssertString(RouterClicks.Prompt(_router, deck)).IsEqual("Unstrap the deck (3 cards)");
        AssertBool(RouterClicks.Click(_router, deck)).IsTrue();

        AssertThat(_hand.Cards.OfType<CardController>().Select(card => card.ShopPrice).ToArray())
            .IsEqual(new int?[] { 10, 11, 12 });
        AssertThat(_hand.Cards.OfType<CardController>().Select(card => card.LookSeed).ToArray())
            .IsEqual(new int?[] { 100, 101, 102 });
        AssertBool(deck.IsInsideTree()).IsFalse();
        AssertThat(Bundles().Count()).IsEqual(0);
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public async Task ADeckThatDoesNotFitInTheHandStaysStrapped()
    {
        HoldCards(3);
        var deck = _bundler.Strap()!;
        HoldCards(IHand.MostPlaces - 2);

        AssertObject(RouterClicks.Prompt(_router, deck)).IsNull();
        AssertBool(RouterClicks.Click(_router, deck)).IsFalse();

        AssertThat(deck.Count).IsEqual(3);
        AssertThat(_hand.Count).IsEqual(IHand.MostPlaces - 2);
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public void APlainClickOnAPileTakesItsLastCard()
    {
        var pile = CardBundle.Create(CardBundleKind.Pile,
            Enumerable.Range(0, 5).Select(i => new StoredCard(null, i, i)));
        _world.AddChild(pile);

        AssertString(RouterClicks.Prompt(_router, pile)).IsEqual("Take 1 card from the pile (5 in it)");
        AssertBool(RouterClicks.Click(_router, pile)).IsTrue();

        AssertThat(_hand.Cards.OfType<CardController>().Select(card => card.LookSeed).ToArray())
            .IsEqual(new int?[] { 4 });
        AssertThat(pile.Count).IsEqual(4);
    }

    [TestCase]
    public void AShiftClickOnAPileGivesTheHandTheLastCardsInAsManyAsFit()
    {
        var pile = CardBundle.Create(CardBundleKind.Pile,
            Enumerable.Range(0, 5).Select(i => new StoredCard(null, i, i)));
        _world.AddChild(pile);
        HoldCards(IHand.MostPlaces - 3);

        AssertString(RouterClicks.Prompt(_router, pile, shift: true)).IsEqual("Take 3 cards from the pile (5 in it)");
        AssertBool(RouterClicks.Click(_router, pile, shift: true)).IsTrue();

        // The same as three plain clicks: the last card in comes first.
        AssertThat(_hand.Count).IsEqual(IHand.MostPlaces);
        AssertThat(_hand.Cards.OfType<CardController>().TakeLast(3).Select(card => card.LookSeed).ToArray())
            .IsEqual(new int?[] { 4, 3, 2 });
        AssertThat(pile.Cards.Select(card => card.LookSeed).ToArray()).IsEqual(new int?[] { 0, 1 });
        AssertObject(RouterClicks.Prompt(_router, pile)).IsNull();
    }

    [TestCase]
    public void AFullHandRefusesTheNextCard()
    {
        HoldCards(IHand.MostPlaces);
        var extra = ShopTestCards.Create(null);
        _world.AddChild(extra);

        AssertBool(_hand.TryTake(extra)).IsFalse();

        AssertThat(_hand.Count).IsEqual(IHand.MostPlaces);
        AssertThat(_hand.Room).IsEqual(0);
        AssertObject(extra.GetParent()).IsSame(_world);
    }

    private CardController[] HoldCards(int count)
    {
        var start = _hand.Count;
        var cards = new CardController[count];
        for (var i = 0; i < count; i++)
        {
            var n = start + i;
            var card = ShopTestCards.Create(new CardSignature([0.1f * (n + 1), 0, 0, 0, 0, 0, 0, 0]));
            card.ShopPrice = 10 + n;
            card.LookSeed = 100 + n;
            _world.AddChild(card);
            AssertBool(_hand.TryTake(card)).IsTrue();
            cards[i] = card;
        }

        return cards;
    }

    private IOrderedEnumerable<CardBundle> Bundles() =>
        _world.GetTree().GetNodesInGroup(CardBundle.GroupName).OfType<CardBundle>()
            .Where(bundle => bundle.IsInsideTree()).OrderBy(bundle => bundle.Name.ToString());

    private static bool IsInstanceValid(GodotObject node) => GodotObject.IsInstanceValid(node);
}
