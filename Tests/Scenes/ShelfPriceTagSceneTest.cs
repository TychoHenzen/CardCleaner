using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Price tags on the shelf scenes: a card shelf has one tag per seat, a product shelf one per board, which prints the
/// price every item on the board asks, and a storage rack none, since nothing on it is sold. The interaction ray
/// reaches the tag and the card as two separate targets.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShelfPriceTagSceneTest
{
    private const string ShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string StorageRackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const uint InteractableMask = 6;
    private const uint TagLayer = 4;

    [BeforeTest]
    public static void Setup()
    {
        TestServices.Reset();
    }

    [TestCase]
    public static async Task ACardShelfHasOneTagPerSeat()
    {
        var shelf = LoadShelf(ShelfScene);
        await Settle();

        var slots = shelf.Slots.ToList();
        AssertThat(shelf.Pricings.Count()).IsEqual(slots.Count);
        AssertThat(shelf.Pricings.Select(pricing => pricing.Slot).Distinct().Count()).IsEqual(slots.Count);
        foreach (var slot in slots)
            AssertThat(shelf.PricingFor(slot)!.Slot).IsEqual(slot);
    }

    [TestCase]
    public static async Task AProductShelfHasOneTagPerBoardThatEverySeatOfTheBoardShares()
    {
        var shelf = LoadShelf(ProductShelfScene);
        await Settle();

        var levels = shelf.Levels.ToList();
        AssertThat(shelf.Pricings.Count()).IsEqual(levels.Count);
        AssertThat(shelf.Pricings.Select(pricing => pricing.Level).Distinct().Count()).IsEqual(levels.Count);
        foreach (var level in levels)
        {
            var tag = shelf.PricingFor(level)!;
            AssertThat(tag.Level).IsEqual(level);
            AssertBool(level.Seats.All(seat => shelf.PricingFor(seat) == tag)).IsTrue();
        }
    }

    [TestCase]
    public static async Task AStorageRackHasNoPriceTags()
    {
        var shelf = LoadShelf(StorageRackScene);
        await Settle();

        AssertThat(shelf.Pricings.Count()).IsEqual(0);
        AssertObject(shelf.PricingFor(shelf.Slots.First())).IsNull();
    }

    [TestCase]
    public static async Task AStockedCardShelfSlotPrintsTheCardPrice()
    {
        var shelf = LoadShelf(ShelfScene);
        var slot = shelf.Slots.First();
        var pricing = shelf.PricingFor(slot)!;
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        slot.TryLock(card);
        await Settle();

        AssertThat(pricing.TagLabel!.Text).IsEqual($"{CardValue.MinimumCardPrice}");

        pricing.PricedStock()!.SetPrice(5);
        AssertThat(pricing.TagLabel.Text).IsEqual("5");

        slot.ReleaseCard(card);
        AssertThat(pricing.TagLabel.Text).IsEqual(string.Empty);
    }

    [TestCase]
    public static async Task AProductShelfBoosterShowsTheBoosterPrice()
    {
        var shelf = LoadShelf(ProductShelfScene);
        var level = shelf.Levels.First();
        var booster = AddNode(Load<CardContainer>(BoosterScene));
        AssertBool(level.TryLock(booster)).IsTrue();
        await Settle();

        AssertThat(shelf.PricingFor(level)!.TagLabel!.Text)
            .IsEqual($"{ProductMarketPrices.BoosterPrice}");
    }

    // A booster box on a board shows its price on the board's tag; an empty board's tag prints nothing.
    [TestCase]
    public static async Task AProductShelfBoardPrintsItsBoosterBoxPriceAndAnEmptyBoardNone()
    {
        var shelf = LoadShelf(ProductShelfScene);
        var levels = shelf.Levels.ToList();
        var pack = AddNode(Load<CardContainer>(PackScene));
        AssertBool(levels[0].TryLock(pack)).IsTrue();
        await Settle();

        AssertThat(shelf.PricingFor(levels[0])!.TagLabel!.Text).IsEqual($"{ProductMarketPrices.PackPrice}");
        var emptyTag = shelf.PricingFor(levels[1])!;
        AssertThat(emptyTag.TagLabel!.Text).IsEqual(string.Empty);
        AssertBool(emptyTag.CanInteract).IsFalse();
    }

    // The board's click target stands in front of its stock, so the ray at a shelved card reaches the board, whose
    // click takes the card; the tag stays a target of its own.
    [TestCase]
    public static async Task TheRayPicksTheTagAtTheTagAndTheBoardAtTheCard()
    {
        var shelf = LoadShelf(ShelfScene);
        var level = shelf.Levels.First();
        var slot = level.Seats[0];
        var tag = shelf.PricingFor(slot)!;
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        AssertBool(level.TryLock(card)).IsTrue();
        await Settle();

        AssertBool(tag.CollisionLayer == TagLayer).IsTrue();
        AssertBool(tag.CollisionMask == 0).IsTrue();

        var space = shelf.GetWorld3D().DirectSpaceState;
        var tagHit = Climb(Cast(
            space, tag.GlobalPosition + new Vector3(0, 0, -1), tag.GlobalPosition + new Vector3(0, 0, 1)));
        AssertBool(ReferenceEquals(tagHit, tag)).IsTrue();

        var cardHit = Climb(Cast(
            space, card.GlobalPosition + new Vector3(0, 1, 0), card.GlobalPosition + new Vector3(0, -1, 0)));
        var board = shelf.LevelTargets.Single(target => target.Level == level);
        AssertBool(ReferenceEquals(cardHit, board))
            .OverrideFailureMessage($"the ray at the card hit {(cardHit as Node)?.Name}, expected {board.Name}")
            .IsTrue();
    }

    private static T Load<T>(string path) where T : Node
    {
        return GD.Load<PackedScene>(path).Instantiate<T>();
    }

    private static CardShelf LoadShelf(string path)
    {
        var shelf = AddNode(Load<CardShelf>(path));
        shelf.Freeze = true;
        return shelf;
    }

    private static Task Settle() => BoundedWait.SyncPhysicsFrames(2);

    /// <summary>The collider the interaction ray hits, with the same mask the player's interaction uses.</summary>
    private static Node? Cast(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 to)
    {
        var query = new PhysicsRayQueryParameters3D
        {
            CollideWithBodies = true,
            CollideWithAreas = false,
            CollisionMask = InteractableMask,
            From = from,
            To = to,
        };
        var hit = space.IntersectRay(query);
        return hit.Count == 0 ? null : hit["collider"].As<Node>();
    }

    /// <summary>Walks up from the collider to the first interactable or card, as the interaction system does.</summary>
    private static Node? Climb(Node? node)
    {
        for (var current = node; current != null; current = current.GetParent())
        {
            if (current is IInteractable or CardController) return current;
        }

        return null;
    }
}
