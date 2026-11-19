using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using GdUnit4;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.CardStacks.Components;

/// <summary>
///     The hand's side of decks and piles: V straps the cards in hand into one deck that keeps every card, a click on
///     a deck unstraps it into the hand when it fits, and a click on a pile takes as many cards as fit.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardBundlerTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";

    private Node3D _world = null!;
    private Camera3D _eye = null!;
    private Mocking.MockCardSpawner _spawnerNode = null!;
    private ShopTestCardSpawner _spawning = null!;
    private CardHolder _hand = null!;
    private ContainerHolder _containers = null!;
    private CardBundler _bundler = null!;
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
        _hand = TestServices.Inject(new CardHolder { Name = "Hand" });
        var dropper = TestServices.Inject(new CardDropper());
        _containers = TestServices.Inject(new ContainerHolder());
        // In the world, so it is freed with it; a node left outside the tree outlives the test.
        _world.AddChild(interaction);
        _world.AddChild(_hand);
        _world.AddChild(dropper);
        _world.AddChild(_containers);
        _hand.SetReferences(_eye);
        _containers.SetReferences(_eye, _hand);

        _bundler = TestServices.Inject(new CardBundler
        {
            InteractionSystem = interaction,
            CardHolder = _hand,
            CardDropper = dropper,
            ContainerHolderNode = _containers,
        });
        _world.AddChild(_bundler);
    }

    [AfterTest]
    public static void Cleanup() => TestServices.Reset();

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheStrapKeyStrapsEveryCardInHandIntoOneDeckInFrontOfThePlayer()
    {
        var cards = HoldCards(3);

        _strapKey!.Invoke();

        AssertThat(_hand.HeldCount).IsEqual(0);
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
    [TestCategory("Unit")]
    public async Task ACardWithoutASignatureStaysWithoutOneThroughADeck()
    {
        var card = ShopTestCards.Create(null);
        _world.AddChild(card);
        _hand.AddCard(card);
        HoldCards(1);

        _bundler.Strap()!.Interact();

        AssertBool(_hand.HeldCards.OfType<CardController>().First().HasSignature).IsFalse();
        AssertBool(_hand.HeldCards.OfType<CardController>().Last().HasSignature).IsTrue();
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OneCardIsNotStrapped()
    {
        HoldCards(1);

        AssertBool(_bundler.CanStrap).IsFalse();
        AssertObject(_bundler.Strap()).IsNull();
        AssertThat(_hand.HeldCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NothingIsStrappedWhileAContainerIsHeld()
    {
        HoldCards(2);
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        _world.AddChild(box);
        _containers.TryPickUp(box);

        AssertObject(_bundler.Strap()).IsNull();
        AssertThat(_hand.HeldCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ClickingADeckUnstrapsItIntoTheHandInTheOrderItWasStrapped()
    {
        HoldCards(3);
        var deck = _bundler.Strap()!;

        AssertString(deck.Prompt).IsEqual("Unstrap the deck (3 cards)");
        deck.Interact();

        AssertThat(_hand.HeldCards.OfType<CardController>().Select(card => card.ShopPrice).ToArray())
            .IsEqual(new int?[] { 10, 11, 12 });
        AssertThat(_hand.HeldCards.OfType<CardController>().Select(card => card.LookSeed).ToArray())
            .IsEqual(new int?[] { 100, 101, 102 });
        AssertBool(deck.IsInsideTree()).IsFalse();
        AssertThat(Bundles().Count()).IsEqual(0);
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ADeckThatDoesNotFitInTheHandStaysStrapped()
    {
        HoldCards(3);
        var deck = _bundler.Strap()!;
        HoldCards(CardHolder.MostCards - 2);

        AssertBool(deck.CanInteract).IsFalse();
        deck.Interact();

        AssertThat(deck.Count).IsEqual(3);
        AssertThat(_hand.HeldCount).IsEqual(CardHolder.MostCards - 2);
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void APileGivesTheHandTheLastCardsInAsManyAsFit()
    {
        var pile = CardBundle.Create(CardBundleKind.Pile,
            Enumerable.Range(0, 5).Select(i => new StoredCard(null, i, i)));
        _world.AddChild(pile);
        HoldCards(CardHolder.MostCards - 3);

        AssertString(pile.Prompt).IsEqual("Take 3 cards from the pile (5 in it)");
        pile.Interact();

        AssertThat(_hand.HeldCount).IsEqual(CardHolder.MostCards);
        AssertThat(_hand.HeldCards.OfType<CardController>().TakeLast(3).Select(card => card.LookSeed).ToArray())
            .IsEqual(new int?[] { 2, 3, 4 });
        AssertThat(pile.Cards.Select(card => card.LookSeed).ToArray()).IsEqual(new int?[] { 0, 1 });
        AssertBool(pile.CanInteract).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AFullHandRefusesTheNextCard()
    {
        HoldCards(CardHolder.MostCards);
        var extra = ShopTestCards.Create(null);
        _world.AddChild(extra);

        AssertBool(_hand.AddCard(extra)).IsFalse();

        AssertThat(_hand.HeldCount).IsEqual(CardHolder.MostCards);
        AssertThat(_hand.Room).IsEqual(0);
        AssertObject(extra.GetParent()).IsSame(_world);
    }

    private CardController[] HoldCards(int count)
    {
        var start = _hand.HeldCount;
        var cards = new CardController[count];
        for (var i = 0; i < count; i++)
        {
            var n = start + i;
            var card = ShopTestCards.Create(new CardSignature([0.1f * (n + 1), 0, 0, 0, 0, 0, 0, 0]));
            card.ShopPrice = 10 + n;
            card.LookSeed = 100 + n;
            _world.AddChild(card);
            AssertBool(_hand.AddCard(card)).IsTrue();
            cards[i] = card;
        }

        return cards;
    }

    private IOrderedEnumerable<CardBundle> Bundles() =>
        _world.GetTree().GetNodesInGroup(CardBundle.GroupName).OfType<CardBundle>()
            .Where(bundle => bundle.IsInsideTree()).OrderBy(bundle => bundle.Name.ToString());

    private static bool IsInstanceValid(GodotObject node) => GodotObject.IsInstanceValid(node);
}
