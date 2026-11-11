using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The restocker in the real shop scene: a hired restocker with the shop open carries a rack pack to the product
///     shelf, unpacks a box there once enough slots are free, and does nothing when it is not hired, the shop is
///     closed, the tree is paused or there is nothing to move.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RestockerTest : RestockerTestBase
{
    [TestCase]
    [TestCategory("Unit")]
    public async Task HiredRestockerMovesARackPackToTheProductShelf()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());

        var moved = await Poll(() => _product.Slots.Any(slot => slot.Cards.Contains(pack)));

        AssertBool(moved).IsTrue();
        AssertBool(_rack.Slots.First().HasCards).IsFalse();
        AssertBool(_restocker.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoxIsUnpackedAtTheRealThresholdAndEveryPackEndsOnAProductShelf()
    {
        HireAndOpen();
        var shelves = AddProductShelves();
        Stock(PackScene, shelves[0].Slots.First());
        var box = Stock(BoxScene, _rack.Slots.First());
        var slotCount = shelves.Sum(shelf => shelf.SlotCount);
        AssertInt(slotCount - 1).IsEqual(_restocker.BoxUnpackThreshold);

        // One shelf takes its free slots at the unpack; the rest spill and are carried to the other shelves.
        var shelved = await Poll(
            () => !GodotObject.IsInstanceValid(box) && shelves.Sum(shelf => shelf.StockedCount) == slotCount,
            LongWalkSeconds);

        AssertBool(shelved).IsTrue();
        AssertInt(PackCount()).IsEqual(PacksInABox + 1);
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HomeReachesTheRackAndTheRackReachesTheProductShelf()
    {
        var region = _shop.GetNode<ShopNavigationRegion>("World/Navigation");
        var map = region.GetNavigationMap();
        var rack = NavigationServer3D.MapGetClosestPoint(map, _rack.GlobalPosition);
        var shelf = NavigationServer3D.MapGetClosestPoint(map, _product.GlobalPosition);

        AssertBool(region.Reaches(_restocker.GlobalPosition, rack)).IsTrue();
        AssertBool(region.Reaches(rack, shelf)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoxStaysOnTheRackAtTheRealThresholdWithOneSlotTooFewFree()
    {
        HireAndOpen();
        var shelves = AddProductShelves();
        foreach (var slot in shelves[0].Slots.Take(2))
            Stock(PackScene, slot);
        var box = Stock(BoxScene, _rack.Slots.First());
        AssertInt(shelves.Sum(shelf => shelf.SlotCount) - 2).IsEqual(_restocker.BoxUnpackThreshold - 1);

        await Frames(LongIdleFrames);

        AssertBool(GodotObject.IsInstanceValid(box) && _rack.Slots.First().Cards.Contains(box)).IsTrue();
        AssertInt(PackCount()).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task DefaultThresholdLeavesABoxOnTheRackWithOneThreeSlotShelf()
    {
        HireAndOpen();
        var box = Stock(BoxScene, _rack.Slots.First());

        await Frames(LongIdleFrames);

        AssertBool(_rack.Slots.First().Cards.Contains(box)).IsTrue();
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ClosedShopLeavesTheRestockerWhereItStands()
    {
        Hire();
        var pack = Stock(PackScene, _rack.Slots.First());
        await AssertStaysPut(pack);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PausedTreeLeavesTheRestockerWhereItStands()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());
        _tree.Paused = true;

        await AssertStaysPut(pack);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task UnhiredRestockerNeverMovesAndIsHidden()
    {
        _day.OpenShop();
        var pack = Stock(PackScene, _rack.Slots.First());

        AssertBool(_restocker.Visible).IsFalse();
        await AssertStaysPut(pack);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task NothingToRestockStaysIdleAtHome()
    {
        HireAndOpen();
        var home = _restocker.GlobalPosition;

        await Frames(IdleFrames);

        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
        AssertThat(_restocker.GlobalPosition).IsEqual(home);
    }

}
