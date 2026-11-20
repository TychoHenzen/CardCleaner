using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The restocker in the real shop scene: a hired restocker with the shop open carries a rack pack to the product
///     shelf, tops up a part-filled booster stack, unpacks a box there once enough slots are free (finishing a partly
///     emptied box at the next shelf), and
///     does nothing when it is not hired, the shop is closed, the tree is paused or there is nothing to move.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RestockerTest : RestockerTestBase
{
    [TestCase]
    public async Task HiredRestockerMovesARackPackToTheProductShelf()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());

        var moved = await Poll(() => _product.Slots.Any(slot => slot.Cards.Contains(pack)));

        AssertBool(moved).IsTrue();
        AssertBool(_rack.Slots.First().HasCards).IsFalse();
        AssertBool(_restocker.Visible).IsTrue();
    }

    // Customers take boosters off a stack one at a time; the restocker tops a part-filled stack up from the rack, and
    // the board's first seat, the stack, takes it before the board's empty seats. Every other board holds booster
    // boxes, since a board holds one kind.
    [TestCase]
    public async Task HiredRestockerTopsUpAPartFilledBoosterStack()
    {
        HireAndOpen();
        var boosters = _product.Levels.First();
        foreach (var level in _product.Levels.Skip(1))
        {
            foreach (var _ in level.Seats)
                StockOn(PackScene, level);
        }

        var shelved = StockOn(BoosterScene, boosters);
        var stack = boosters.Seats[0];
        var booster = Stock(BoosterScene, _rack.Slots.First());

        var toppedUp = await Poll(() => stack.Cards.Contains(booster));

        AssertBool(toppedUp).IsTrue();
        AssertInt(stack.Cards.Count).IsEqual(2);
        AssertBool(stack.Cards.Contains(shelved)).IsTrue();
    }

    [TestCase]
    public async Task BoxIsUnpackedAtTheRealThresholdAndEveryPackEndsOnAProductShelf()
    {
        HireAndOpen();
        var shelves = AddProductShelves();
        var stocked = StockAllBut(shelves, _restocker.BoxUnpackThreshold);
        var box = Stock(BoxScene, _rack.Slots.First());
        var slotCount = shelves.Sum(shelf => shelf.SlotCount);

        // One shelf takes its free slots at the first unpack; the restocker carries the box on and empties it into
        // the other shelf, then puts the empty box back on a rack for the player to throw away.
        var shelved = await Poll(
            () => shelves.Sum(shelf => shelf.StockedCount) == slotCount && _rack.Slots.Any(s => s.Cards.Contains(box)),
            LongWalkSeconds);

        AssertBool(shelved).IsTrue();
        AssertInt(box.Remaining).IsEqual(0);
        AssertInt(PackCount()).IsEqual(PacksInABox + stocked);
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
    }

    [TestCase]
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
    public async Task BoxStaysOnTheRackAtTheRealThresholdWithOneSlotTooFewFree()
    {
        HireAndOpen();
        var shelves = AddProductShelves();
        var stocked = StockAllBut(shelves, _restocker.BoxUnpackThreshold - 1);
        var box = Stock(BoxScene, _rack.Slots.First());

        await Frames(LongIdleFrames);

        AssertBool(GodotObject.IsInstanceValid(box) && _rack.Slots.First().Cards.Contains(box)).IsTrue();
        AssertInt(PackCount()).IsEqual(stocked);
    }

    [TestCase]
    public async Task DefaultThresholdUnpacksABoxOntoTheEmptyProductShelf()
    {
        HireAndOpen();
        Stock(BoxScene, _rack.Slots.First());

        var filled = await Poll(() => _product.StockedCount == PacksInABox, LongWalkSeconds);

        AssertBool(filled).IsTrue();
        AssertInt(PackCount()).IsEqual(PacksInABox);
    }

    [TestCase]
    public async Task ClosedShopLeavesTheRestockerWhereItStands()
    {
        Hire();
        var pack = Stock(PackScene, _rack.Slots.First());
        await AssertStaysPut(pack);
    }

    [TestCase]
    public async Task PausedTreeLeavesTheRestockerWhereItStands()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());
        _tree.Paused = true;

        await AssertStaysPut(pack);
    }

    [TestCase]
    public async Task UnhiredRestockerNeverMovesAndIsHidden()
    {
        _day.OpenShop();
        var pack = Stock(PackScene, _rack.Slots.First());

        AssertBool(_restocker.Visible).IsFalse();
        await AssertStaysPut(pack);
    }

    [TestCase]
    public async Task NothingToRestockStaysIdleAtHome()
    {
        HireAndOpen();
        var home = _restocker.GlobalPosition;

        await Frames(IdleFrames);

        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
        AssertThat(_restocker.GlobalPosition).IsEqual(home);
    }

}
