using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Customers;
using CardCleaner.Scripts.Features.Shop.Shelves;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Customers;

[TestSuite]
[RequireGodotRuntime]
public class ShelfOfferIndexTest
{
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string StorageRackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";

    // How far in front of a level a customer stands, as ShelfOfferIndex sets it.
    private const float ApproachMeters = 0.8f;
    private const float Tolerance = 0.001f;

    private CardShelf _shelf = null!;
    private ShelfOfferIndex _index = null!;

    [BeforeTest]
    public async Task Setup()
    {
        TestServices.Reset();
        // Frozen as PlacedFurniture freezes every shelf in the shop. A loose shelf would fall, and a frozen booster
        // locked into its level lies inside the shelf's collision box, so it would push the shelf away.
        _shelf = GD.Load<PackedScene>(ProductShelfScene).Instantiate<CardShelf>();
        _shelf.Freeze = true;
        AddNode(_shelf);
        await BoundedWait.SyncPhysicsFrames(2);
        _index = new ShelfOfferIndex(_shelf.GetTree());
    }

    [TestCase]
    public void Empty_shelf_has_no_offers()
    {
        AssertThat(_index.Collect().Count).IsEqual(0);
    }

    [TestCase]
    public async Task Stocked_booster_is_one_offer()
    {
        var level = _shelf.Levels.First();
        await Stock(level);

        var offers = _index.Collect();

        AssertThat(offers.Count).IsEqual(1);
        AssertThat(offers[0].OfferId).IsEqual(level.GetPath().ToString());
        AssertThat(offers[0].AskingPrice).IsEqual(4);
        AssertThat(offers[0].MarketValue).IsEqual(4);
    }

    [TestCase]
    public async Task The_price_set_on_the_stock_is_the_asking_price()
    {
        var level = _shelf.Levels.First();
        await Stock(level);
        ((CardContainer)level.Last!).ShopPrice = 6;

        var offer = _index.Collect().Single();

        AssertThat(offer.AskingPrice).IsEqual(6);
        AssertThat(offer.MarketValue).IsEqual(4);
    }

    [TestCase]
    public async Task Storage_rack_stock_is_not_offered()
    {
        var rack = AddNode(GD.Load<PackedScene>(StorageRackScene).Instantiate<CardShelf>());
        await BoundedWait.SyncPhysicsFrames(2);
        var booster = MakeContainer();
        AssertBool(rack.Levels.First().TryLock(booster)).IsTrue();
        await BoundedWait.SyncPhysicsFrames(2);

        AssertThat(rack.Levels.First().Count).IsEqual(1);
        AssertThat(_index.Collect().Count).IsEqual(0);
    }

    [TestCase]
    public async Task LevelFor_maps_an_offer_id_back()
    {
        var level = _shelf.Levels.First();
        await Stock(level);
        var offerId = _index.Collect().Single().OfferId;

        AssertBool(ReferenceEquals(_index.LevelFor(offerId), level)).IsTrue();
        AssertBool(_index.LevelFor("/root/none") is null).IsTrue();
    }

    [TestCase]
    public async Task ApproachPoint_is_in_front_of_the_level()
    {
        _shelf.GlobalPosition = Vector3.Zero;
        _shelf.GlobalRotation = Vector3.Zero;
        var level = _shelf.Levels.First();
        await Stock(level);
        var offerId = _index.Collect().Single().OfferId;

        var point = _index.ApproachPointFor(offerId);

        AssertBool(point.HasValue).IsTrue();
        // The shelf stands at the origin unturned, so the level's own position in the shelf is its world X and Z, and
        // the point lies ApproachMeters in front of it (towards -Z), on the floor the shelf stands on.
        var expected = new Vector3(level.Position.X, 0f, level.Position.Z - ApproachMeters);
        AssertVector(point!.Value).IsEqualApprox(expected, new Vector3(Tolerance, Tolerance, Tolerance));
        AssertBool(_index.ApproachPointFor("/root/none") is null).IsTrue();
    }

    [TestCase]
    public async Task BrowsePoints_cover_every_shown_sales_level_and_skip_racks_and_hidden_shelves()
    {
        var rack = AddNode(GD.Load<PackedScene>(StorageRackScene).Instantiate<CardShelf>());
        await BoundedWait.SyncPhysicsFrames(2);

        var points = _index.BrowsePoints();

        AssertThat(points.Count).IsEqual(_shelf.Levels.Count());
        var firstLevel = _shelf.Levels.First();
        AssertBool(points[0].DistanceTo(_index.ApproachPointFor(firstLevel.GetPath().ToString())!.Value) < 0.001f)
            .IsTrue();

        _shelf.Visible = false;
        AssertThat(_index.BrowsePoints().Count).IsEqual(0);
        AssertBool(rack.IsInsideTree()).IsTrue();
    }

    private static async Task Stock(ShelfLevel level)
    {
        var booster = MakeContainer();
        AssertBool(level.TryLock(booster)).IsTrue();
        await BoundedWait.SyncPhysicsFrames(2);
    }

    private static CardContainer MakeContainer()
    {
        return AddNode(GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>());
    }

}
