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
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Price tags on the shelf scenes: every slot has its own tag, the tag shows the price of its stock, and the
/// interaction ray reaches the tag and the card as two separate targets.
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
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const uint InteractableMask = 6;
    private const uint TagLayer = 4;
    private const int MarkupStep = 10;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task EveryTaggedShelfSceneHasOneTagPerSlot()
    {
        foreach (var path in new[] { ShelfScene, ProductShelfScene, StorageRackScene })
        {
            var shelf = LoadShelf(path);
            await Settle();

            var slots = shelf.Slots.ToList();
            AssertThat(shelf.Pricings.Count()).IsEqual(slots.Count);
            AssertThat(shelf.Pricings.Select(pricing => pricing.Slot).Distinct().Count()).IsEqual(slots.Count);
            foreach (var slot in slots)
            {
                AssertThat(shelf.PricingFor(slot)).IsNotNull();
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AStockedCardShelfSlotShowsTheCardReadoutAndInteractSteps()
    {
        var shelf = LoadShelf(ShelfScene);
        var slot = shelf.Slots.First();
        var pricing = shelf.PricingFor(slot)!;
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        slot.TryLock(card);
        await Settle();

        AssertThat(pricing.TagLabel!.Text).IsEqual(AskingPrice.Readout(CardValue.MinimumCardPrice, 0));

        pricing.Interact();
        AssertThat(pricing.TagLabel.Text).IsEqual(AskingPrice.Readout(CardValue.MinimumCardPrice, MarkupStep));

        slot.ReleaseCard(card);
        AssertThat(pricing.TagLabel.Text).IsEqual(string.Empty);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AProductShelfBoosterShowsTheBoosterPrice()
    {
        var shelf = LoadShelf(ProductShelfScene);
        var slot = shelf.Slots.First();
        var booster = AddNode(Load<CardContainer>(BoosterScene));
        AssertBool(slot.TryLock(booster)).IsTrue();
        await Settle();

        AssertThat(shelf.PricingFor(slot)!.TagLabel!.Text)
            .IsEqual(AskingPrice.Readout(ProductMarketPrices.BoosterPrice, 0));
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AStorageRackPackShowsThePackPriceAndABoxShowsNone()
    {
        var shelf = LoadShelf(StorageRackScene);
        var slots = shelf.Slots.ToList();
        var pack = AddNode(Load<CardContainer>(PackScene));
        var box = AddNode(Load<CardContainer>(BoxScene));
        AssertBool(slots[0].TryLock(pack)).IsTrue();
        AssertBool(slots[1].TryLock(box)).IsTrue();
        await Settle();

        var packTag = shelf.PricingFor(slots[0])!;
        var boxTag = shelf.PricingFor(slots[1])!;
        AssertThat(packTag.TagLabel!.Text).IsEqual(AskingPrice.Readout(ProductMarketPrices.PackPrice, 0));
        AssertThat(boxTag.TagLabel!.Text).IsEqual(string.Empty);
        AssertBool(boxTag.CanInteract).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheRayPicksTheTagAtTheTagAndTheCardAtTheCard()
    {
        var shelf = LoadShelf(ShelfScene);
        var slot = shelf.Slots.First();
        var tag = shelf.PricingFor(slot)!;
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        slot.TryLock(card);
        await Settle();

        AssertBool(tag.CollisionLayer == TagLayer).IsTrue();
        AssertBool(tag.CollisionMask == 0).IsTrue();

        var space = shelf.GetWorld3D().DirectSpaceState;
        var tagHit = Climb(Cast(space, tag.GlobalPosition + new Vector3(0, 0, -1), tag.GlobalPosition + new Vector3(0, 0, 1)));
        AssertBool(ReferenceEquals(tagHit, tag)).IsTrue();

        var cardHit = Climb(Cast(space, card.GlobalPosition + new Vector3(0, 1, 0), card.GlobalPosition + new Vector3(0, -1, 0)));
        AssertBool(ReferenceEquals(cardHit, card)).IsTrue();
    }

    private static T Load<T>(string path) where T : Node
    {
        return GD.Load<PackedScene>(path).Instantiate<T>();
    }

    private CardShelf LoadShelf(string path)
    {
        var shelf = AddNode(Load<CardShelf>(path));
        shelf.Freeze = true;
        return shelf;
    }

    private static async Task Settle()
    {
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

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
