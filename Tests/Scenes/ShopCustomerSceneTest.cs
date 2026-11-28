using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using static CardCleaner.Tests.Scenes.ShopCustomerRig;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Customers in the shop scene: a customer walks in from the street, takes the stocked item at its market price,
/// pays at the counter and leaves. Customers that cannot reach their target or get stuck give up and free their claim.
/// Arrivals that follow the shop day are covered by <see cref="ShopCustomerDaySceneTest"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCustomerSceneTest
{
    private const float TakeLeaveTolerance = 1.0f;
    private const float SpawnTolerance = 0.01f;
    private const string CardShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";

    private ShopCustomerRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopCustomerRig.Create();
    }

    [AfterTest]
    public void Teardown()
    {
        _rig?.Dispose();
        TestServices.Reset();
    }

    [TestCase]
    public async Task CustomerBuysTheStockedItemAtMarketPrice()
    {
        var slot = _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow();
        AssertThat(customer).IsNotNull();
        AssertBool(XzDistance(customer!.GlobalPosition, _rig.Marker("CustomerSpawn")) <= SpawnTolerance).IsTrue();

        var last = customer.GlobalPosition;
        for (var i = 0; i < FrameBudget && !Gone(customer); i++)
        {
            last = customer.GlobalPosition;
            await ISceneRunner.SyncPhysicsFrame;
        }

        AssertBool(Gone(customer)).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + 4);
        AssertBool(slot.HasCards).IsFalse();
        AssertThat(_rig.Complaints.Reasons.Count).IsEqual(0);
        AssertBool(XzDistance(last, _rig.Marker("CustomerDespawn")) <= TakeLeaveTolerance).IsTrue();
        AssertThat(_rig.Spawner.ClaimedCount).IsEqual(0);
    }

    [TestCase]
    public async Task CustomerRefusesAnOverpricedItem()
    {
        var slot = _rig.StockBooster();
        ((CardContainer)slot.Cards[^1]).ShopPrice = ProductMarketPrices.BoosterPrice * 2;
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
        AssertThat(string.Join("|", _rig.Complaints.Reasons)).IsEqual("price too high");
    }

    [TestCase]
    public async Task TwoCustomersNeverTakeOneItem()
    {
        _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var first = _rig.Spawner.SpawnNow()!;
        var second = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(first) && Gone(second));

        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + 4);
        AssertThat(string.Join("|", _rig.Complaints.Reasons)).IsEqual("nothing in stock");
    }

    [TestCase]
    public async Task ACustomerInAnEmptyShopLooksAtAShelfComplainsAndLeaves()
    {
        var browsePoints = new ShelfOfferIndex(_rig.Shop.GetTree()).BrowsePoints();
        AssertBool(browsePoints.Count > 0).IsTrue();

        var customer = _rig.Spawner.SpawnNow()!;
        var nearest = float.MaxValue;
        var lookedFrames = 0;
        for (var i = 0; i < FrameBudget && !Gone(customer); i++)
        {
            var position = customer.GlobalPosition;
            nearest = Mathf.Min(nearest, browsePoints.Min(point => XzDistance(position, point)));
            if (customer.State == CustomerState.Browsing && customer.Velocity.IsZeroApprox())
                lookedFrames++;
            await ISceneRunner.SyncPhysicsFrame;
        }

        AssertBool(Gone(customer)).IsTrue();
        AssertBool(nearest <= TakeLeaveTolerance).IsTrue();
        // At 60 physics frames a second, a two-second look is about 120 frames; half of it is enough to prove a pause.
        AssertBool(lookedFrames >= 60).IsTrue();
        AssertThat(string.Join("|", _rig.Complaints.Reasons)).IsEqual("nothing in stock");
    }

    [TestCase]
    public async Task UnreachableTargetMakesTheCustomerLeaveAndFreesTheClaim()
    {
        var slot = _rig.StockBooster();
        var frontBlocker =
            ShopNavigationRig.AddDoorBlocker(_rig.Shop, "FrontDoorBlocker", ShopNavigationRig.FrontDoorX);
        var roadBlocker = ShopNavigationRig.AddDoorBlocker(_rig.Shop, "RoadDoorBlocker", ShopNavigationRig.RoadDoorX);
        await _rig.RebakeAndSync();
        var balanceBefore = _rig.Money.Balance;

        var blocked = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(blocked));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
        AssertThat(_rig.Complaints.Reasons.Count).IsEqual(0);
        AssertThat(_rig.Spawner.ClaimedCount).IsEqual(0);

        ShopNavigationRig.RemoveBlocker(frontBlocker);
        ShopNavigationRig.RemoveBlocker(roadBlocker);
        await _rig.RebakeAndSync();

        var next = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(next));

        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + 4);
    }

    [TestCase]
    public async Task StuckCustomerGivesUpAndLeaves()
    {
        var slot = _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow()!;
        customer.Speed = 0f;
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
        AssertThat(_rig.Spawner.ClaimedCount).IsEqual(0);
    }

    [TestCase]
    public async Task CustomerThatGivesUpAfterTakingReturnsTheItem()
    {
        var slot = _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer) || customer.CarriedCount == 1);
        AssertBool(Gone(customer)).IsFalse();
        AssertBool(slot.HasCards).IsFalse();
        customer.GiveUp();
        await WaitUntil(() => Gone(customer));

        AssertBool(slot.HasCards).IsTrue();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
    }

    [TestCase]
    public async Task CustomerMovesAwayFromTheSpawnWithinHalfASecond()
    {
        _rig.StockBooster();
        var customer = _rig.Spawner.SpawnNow()!;
        await BoundedWait.SyncPhysicsFrames(30);

        AssertBool(Gone(customer)).IsFalse();
        AssertBool(XzDistance(customer.GlobalPosition, _rig.Marker("CustomerSpawn")) >= 2.0f).IsTrue();
    }

    [TestCase]
    public async Task ACustomerFacesTheWayItWalks()
    {
        _rig.StockBooster();
        var customer = _rig.Spawner.SpawnNow()!;
        var walking = 0;
        var facing = 0;
        for (var i = 0; i < FrameBudget && !Gone(customer); i++)
        {
            await ISceneRunner.SyncPhysicsFrame;
            if (Gone(customer))
                break;

            var velocity = new Vector3(customer.Velocity.X, 0f, customer.Velocity.Z);
            if (velocity.Length() < 1f)
                continue;

            walking++;
            var forward = customer.GlobalBasis * Vector3.Forward;
            if (new Vector3(forward.X, 0f, forward.Z).Normalized().Dot(velocity.Normalized()) > 0.9f)
                facing++;
        }

        // A turn takes a few frames at a path corner, so most walking frames, not all, face the walk.
        AssertInt(walking).IsGreater(10);
        AssertFloat((float)facing / walking).IsGreater(0.8f);
    }

    [TestCase]
    public void EachCustomerWearsALookDrawnForIt()
    {
        var worn = new HashSet<int>();
        for (var i = 0; i < 6; i++)
        {
            var customer = _rig.Spawner.SpawnNow()!;
            AssertThat(customer.Figure?.Model).IsNotNull();
            worn.Add(customer.Figure!.Worn);
        }

        AssertInt(worn.Count).IsGreater(1);
    }

    [TestCase]
    public async Task ACustomerStandsStillToPickTheItemUp()
    {
        var slot = _rig.StockBooster();
        var customer = _rig.Spawner.SpawnNow()!;
        var pickingFrames = 0;
        for (var i = 0; i < FrameBudget && !Gone(customer); i++)
        {
            if (customer.Figure!.Playing == FigureClip.PickUp && customer.Velocity.IsZeroApprox())
                pickingFrames++;
            await ISceneRunner.SyncPhysicsFrame;
        }

        AssertBool(Gone(customer)).IsTrue();
        AssertBool(slot.HasCards).IsFalse();
        AssertInt(pickingFrames).IsGreater(0);
    }

    [TestCase]
    public async Task PausedTreeFreezesCustomers()
    {
        _rig.StockBooster();
        var customer = _rig.Spawner.SpawnNow()!;
        await ISceneRunner.SyncPhysicsFrame;

        _rig.Shop.GetTree().Paused = true;
        try
        {
            AssertBool(customer.CanProcess()).IsFalse();
            AssertBool(_rig.Spawner.CanProcess()).IsFalse();
        }
        finally
        {
            _rig.Shop.GetTree().Paused = false;
        }
    }

    [TestCase]
    public async Task CustomerBuysASingleCardFromACardShelf()
    {
        var shelf = GD.Load<PackedScene>(CardShelfScene).Instantiate<CardShelf>();
        shelf.Position = new Vector3(3f, 0f, 9f);
        _rig.Shop.GetNode("World/Storefront").AddChild(shelf);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;

        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        var slot = shelf.Slots.First();
        AssertBool(slot.TryLock(card)).IsTrue();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow()!;
        await WaitUntil(() => Gone(customer));

        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore + CardValue.MinimumCardPrice);
        AssertBool(slot.HasCards).IsFalse();
    }
}
