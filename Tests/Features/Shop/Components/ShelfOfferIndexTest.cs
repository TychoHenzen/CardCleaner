using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class ShelfOfferIndexTest
{
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string StorageRackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";

    // How far in front of a slot a customer stands, as ShelfOfferIndex sets it.
    private const float ApproachMeters = 0.8f;
    private const float Tolerance = 0.001f;

    private CardShelf _shelf = null!;
    private ShelfOfferIndex _index = null!;

    [BeforeTest]
    public async Task Setup()
    {
        // Frozen as PlacedFurniture freezes every shelf in the shop. A loose shelf would fall, and a frozen booster
        // locked into its slot lies inside the shelf's collision box, so it would push the shelf away.
        _shelf = GD.Load<PackedScene>(ProductShelfScene).Instantiate<CardShelf>();
        _shelf.Freeze = true;
        AddNode(_shelf);
        await Frames(2);
        _index = new ShelfOfferIndex(_shelf.GetTree());
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Empty_shelf_has_no_offers()
    {
        AssertThat(_index.Collect().Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Stocked_booster_is_one_offer()
    {
        var slot = _shelf.Slots.First();
        await Stock(slot);

        var offers = _index.Collect();

        AssertThat(offers.Count).IsEqual(1);
        AssertThat(offers[0].OfferId).IsEqual(slot.GetPath().ToString());
        AssertThat(offers[0].AskingPrice).IsEqual(4);
        AssertThat(offers[0].MarketValue).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Markup_changes_the_asking_price()
    {
        var slot = _shelf.Slots.First();
        await Stock(slot);
        _shelf.PricingFor(slot)!.SetMarkup(50);

        var offer = _index.Collect().Single();

        AssertThat(offer.AskingPrice).IsEqual(6);
        AssertThat(offer.MarketValue).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Storage_rack_stock_is_not_offered()
    {
        var rack = AddNode(GD.Load<PackedScene>(StorageRackScene).Instantiate<CardShelf>());
        await Frames(2);
        var booster = MakeContainer();
        AssertBool(rack.Slots.First().TryLock(booster)).IsTrue();
        await Frames(2);

        AssertBool(rack.Slots.First().HasCards).IsTrue();
        AssertThat(_index.Collect().Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SlotFor_maps_an_offer_id_back()
    {
        var slot = _shelf.Slots.First();
        await Stock(slot);
        var offerId = _index.Collect().Single().OfferId;

        AssertBool(ReferenceEquals(_index.SlotFor(offerId), slot)).IsTrue();
        AssertBool(_index.SlotFor("/root/none") is null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ApproachPoint_is_in_front_of_the_slot()
    {
        _shelf.GlobalPosition = Vector3.Zero;
        _shelf.GlobalRotation = Vector3.Zero;
        var slot = _shelf.Slots.First();
        await Stock(slot);
        var offerId = _index.Collect().Single().OfferId;

        var point = _index.ApproachPointFor(offerId);

        AssertBool(point.HasValue).IsTrue();
        // The shelf stands at the origin unturned, so the slot's own position in the shelf is its world X and Z, and
        // the point lies ApproachMeters in front of it (towards -Z), on the floor the shelf stands on.
        var expected = new Vector3(slot.Position.X, 0f, slot.Position.Z - ApproachMeters);
        AssertVector(point!.Value).IsEqualApprox(expected, new Vector3(Tolerance, Tolerance, Tolerance));
        AssertBool(_index.ApproachPointFor("/root/none") is null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BrowsePoints_cover_every_shown_sales_slot_and_skip_racks_and_hidden_shelves()
    {
        var rack = AddNode(GD.Load<PackedScene>(StorageRackScene).Instantiate<CardShelf>());
        await Frames(2);

        var points = _index.BrowsePoints();

        AssertThat(points.Count).IsEqual(_shelf.Slots.Count());
        var firstSlot = _shelf.Slots.First();
        AssertBool(points[0].DistanceTo(_index.ApproachPointFor(firstSlot.GetPath().ToString())!.Value) < 0.001f)
            .IsTrue();

        _shelf.Visible = false;
        AssertThat(_index.BrowsePoints().Count).IsEqual(0);
        AssertBool(rack.IsInsideTree()).IsTrue();
    }

    private async Task Stock(DeckSlot slot)
    {
        var booster = MakeContainer();
        AssertBool(slot.TryLock(booster)).IsTrue();
        await Frames(2);
    }

    private static CardContainer MakeContainer()
    {
        return AddNode(GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>());
    }

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
