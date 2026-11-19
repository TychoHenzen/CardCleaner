using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.CardStacks.Components;

/// <summary>
///     Loose cards heap into piles: more than a hundred cards lying together become one pile that keeps every card, a
///     card at rest on a pile joins it, and two piles that run into each other become one. Cards still moving, in a
///     hand or far away stay loose. The shop runs a gatherer under its scene root.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardPileGathererTest
{
    private const string ShopScene = "res://Scenes/Gameplay/ShopScene.tscn";

    // The cards lie on a grid this far apart, well inside the gatherer's link distance.
    private const float Spacing = 0.1f;
    private const int Columns = 11;

    private Node3D _world = null!;
    private CardPileGatherer _gatherer = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _world = AddNode(new Node3D());
        // Only the explicit Gather calls below gather; the timer never fires during a test.
        _gatherer = new CardPileGatherer { GatherSeconds = float.MaxValue };
        _world.AddChild(_gatherer);
    }

    [AfterTest]
    public static void Cleanup() => TestServices.Reset();

    [TestCase]
    [TestCategory("Unit")]
    public async Task MoreThanAHundredLooseCardsLyingTogetherBecomeOnePileThatKeepsEveryCard()
    {
        var cards = LayCards(CardBundleRules.MostLooseCards + 1, Vector3.Zero);
        var centre = cards.Aggregate(Vector3.Zero, (sum, card) => sum + card.GlobalPosition) / cards.Count;

        _gatherer.Gather();

        var pile = Piles().Single();
        AssertObject(pile.GetParent()).IsSame(_world);
        AssertThat(pile.Count).IsEqual(CardBundleRules.MostLooseCards + 1);
        AssertThat(pile.Cards.Select(card => card.LookSeed).ToArray())
            .IsEqual(Enumerable.Range(0, CardBundleRules.MostLooseCards + 1).Select(i => (int?)i).ToArray());
        AssertThat(pile.Cards.Select(card => card.Price).Last()).IsEqual(CardBundleRules.MostLooseCards + 1);
        AssertThat(pile.Cards[7].Signature!.Value.Febris).IsEqualApprox(0.07f, 0.0001f);
        AssertThat(pile.GlobalPosition.X).IsEqualApprox(centre.X, 0.001f);
        AssertThat(pile.GlobalPosition.Z).IsEqualApprox(centre.Z, 0.001f);
        AssertThat(pile.GlobalPosition.Y).IsBetween(0f, 0.01f);
        AssertBool(cards.All(card => !card.IsInsideTree() && card.IsQueuedForDeletion())).IsTrue();
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AHundredCardsStayLoose()
    {
        var cards = LayCards(CardBundleRules.MostLooseCards, Vector3.Zero);

        _gatherer.Gather();

        AssertThat(Piles().Count).IsEqual(0);
        AssertBool(cards.All(card => card.IsInsideTree())).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ACardStillMovingOrHeldIsNotLooseSoItCountsTowardNoPile()
    {
        var cards = LayCards(CardBundleRules.MostLooseCards + 2, Vector3.Zero);
        cards[0].Freeze = false;
        var eye = new Camera3D();
        _world.AddChild(eye);
        cards[1].Reparent(eye);

        _gatherer.Gather();

        AssertThat(Piles().Count).IsEqual(0);
        AssertBool(cards.All(card => card.IsInsideTree())).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardAtRestOnAPileJoinsItAndOneFarAwayStaysLoose()
    {
        var pile = AddPile(CardBundleRules.MostLooseCards + 1, Vector3.Zero, firstSeed: 0);
        var onTop = LayCards(1, new Vector3(0.2f, 0.1f, 0f), firstSeed: 500).Single();
        var farAway = LayCards(1, new Vector3(5f, 0f, 0f), firstSeed: 600).Single();

        _gatherer.Gather();

        AssertThat(pile.Count).IsEqual(CardBundleRules.MostLooseCards + 2);
        AssertThat(pile.Cards[^1].LookSeed).IsEqual(500);
        AssertBool(onTop.IsInsideTree()).IsFalse();
        AssertBool(farAway.IsInsideTree()).IsTrue();
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TwoPilesThatRunIntoEachOtherBecomeTheFirstPile()
    {
        var first = AddPile(120, Vector3.Zero, firstSeed: 0);
        var second = AddPile(110, new Vector3(0.3f, 0f, 0f), firstSeed: 1000);
        var apart = AddPile(105, new Vector3(10f, 0f, 0f), firstSeed: 2000);

        _gatherer.Gather();

        AssertThat(first.Count).IsEqual(230);
        AssertThat(first.Cards[120].LookSeed).IsEqual(1000);
        AssertBool(second.IsInsideTree()).IsFalse();
        AssertThat(apart.Count).IsEqual(105);
        AssertThat(Piles().Count).IsEqual(2);
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheShopGathersCardsUnderItsSceneRoot()
    {
        var state = GD.Load<PackedScene>(ShopScene).GetState();
        var gatherers = Enumerable.Range(0, state.GetNodeCount())
            .Where(node => Enumerable.Range(0, state.GetNodePropertyCount(node))
                .Any(property => state.GetNodePropertyName(node, property) == "script"
                                 && state.GetNodePropertyValue(node, property).As<Script>().ResourcePath
                                     .EndsWith("/CardPileGatherer.cs")))
            .ToList();

        AssertThat(gatherers).HasSize(1);
        AssertString(state.GetNodePath(gatherers[0], forParent: true).ToString()).IsEqual(".");
    }

    // Frozen cards on a grid from origin, each with its own look seed, price and signature.
    private List<CardController> LayCards(int count, Vector3 origin, int firstSeed = 0)
    {
        var cards = new List<CardController>();
        for (var i = 0; i < count; i++)
        {
            var seed = firstSeed + i;
            var card = ShopTestCards.Create(new CardSignature([0, 0.01f * seed, 0, 0, 0, 0, 0, 0]));
            card.LookSeed = seed;
            card.ShopPrice = seed + 1;
            _world.AddChild(card);
            card.Freeze = true;
            card.GlobalPosition = origin + new Vector3(i % Columns * Spacing, ShopTestCards.CardSize.Y / 2f,
                i / Columns * Spacing);
            cards.Add(card);
        }

        return cards;
    }

    private CardBundle AddPile(int count, Vector3 at, int firstSeed)
    {
        var pile = CardBundle.Create(CardBundleKind.Pile,
            Enumerable.Range(firstSeed, count).Select(seed => new StoredCard(null, seed, null)));
        _world.AddChild(pile);
        pile.GlobalPosition = at;
        return pile;
    }

    private List<CardBundle> Piles() =>
        _world.GetTree().GetNodesInGroup(CardBundle.GroupName).OfType<CardBundle>()
            .Where(bundle => bundle.IsInsideTree() && !bundle.IsQueuedForDeletion()).ToList();
}
