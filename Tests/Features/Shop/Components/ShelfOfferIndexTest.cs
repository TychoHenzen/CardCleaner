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

    private CardShelf _shelf = null!;
    private ShelfOfferIndex _index = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shelf = AddNode(GD.Load<PackedScene>(ProductShelfScene).Instantiate<CardShelf>());
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
        // The shelf is a RigidBody3D that settles a little under y 0, so y is compared to the shelf's own height.
        var expected = new Vector3(-0.7f, _shelf.GlobalPosition.Y, -1.0f);
        AssertBool(point!.Value.DistanceTo(expected) < 0.001f).IsTrue();
        AssertBool(_index.ApproachPointFor("/root/none") is null).IsTrue();
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
