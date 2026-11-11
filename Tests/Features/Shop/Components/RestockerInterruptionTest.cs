using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     What happens to a restocker's load when its work is cut short: dismissal while it carries a container, and a
///     shelf the walk cannot reach.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RestockerInterruptionTest : RestockerTestBase
{
    // The locked expansion lot is walled off from the rest of the shop, so no path leads into it.
    private static readonly Vector3 LockedLotSpot = new(-4f, 0f, 6f);

    [TestCase]
    [TestCategory("Unit")]
    public async Task DismissedRestockerSetsTheCarriedPackDownLooseAtItsFeet()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());
        await Poll(() => IsCarried(pack));

        AssertThat(_staff.Dismiss(StaffDefaults.RestockerId)).IsEqual(StaffResult.Dismissed);

        AssertBool(_restocker.Visible).IsFalse();
        AssertInt(_restocker.GetNode("Hand").GetChildCount()).IsEqual(0);
        AssertThat(pack.GetParent()).IsEqual(_restocker.GetParent());
        AssertBool(pack.Freeze).IsFalse();
        AssertBool(pack.CollisionLayer != 0).IsTrue();
        AssertFloat(pack.GlobalPosition.DistanceTo(_restocker.GlobalPosition)).IsLess(1f);
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
        AssertInt(PackCount()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ShelfBehindTheLockedLotWallSendsTheCarriedPackBackToItsRackSlot()
    {
        _product.GlobalPosition = LockedLotSpot;
        AssertBool(Reaches(_rack, _product)).IsFalse();
        HireAndOpen();
        var origin = _rack.Slots.First();
        var pack = Stock(PackScene, origin);
        await Poll(() => IsCarried(pack));

        // The walk to the lot ends at the wall, short of the shelf: the pair is blocked and the pack goes back.
        var back = await Poll(() => origin.Cards.Contains(pack), LongWalkSeconds);
        await Frames(IdleFrames);

        AssertBool(back).IsTrue();
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
        AssertInt(_product.StockedCount).IsEqual(0);
        AssertBool(origin.Cards.Contains(pack)).IsTrue();
        AssertInt(PackCount()).IsEqual(1);
    }
}
