using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The restocker while it carries a container: it puts a pack or box back on a rack slot when no shelf slot is
///     free, re-plans when a source or target changes on the walk, and holds the load while paused or closed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RestockerCarryTest : RestockerTestBase
{
    [TestCase]
    [TestCategory("Unit")]
    public async Task CarriedPackWithNoFreeShelfSlotGoesBackOnItsRackSlot()
    {
        HireAndOpen();
        var origin = _rack.Slots.First();
        var pack = Stock(PackScene, origin);
        await Poll(() => IsCarried(pack));
        foreach (var slot in _product.Slots)
            Stock(PackScene, slot);

        var back = await Poll(() => origin.Cards.Contains(pack));

        AssertBool(back).IsTrue();
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
        AssertInt(PackCount()).IsEqual(ProductSlots + 1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CarriedBoxBelowTheThresholdGoesBackOnTheRackAndStaysThere()
    {
        HireAndOpen();
        _restocker.BoxUnpackThreshold = ProductSlots;
        var origin = _rack.Slots.First();
        var box = Stock(BoxScene, origin);
        await Poll(() => IsCarried(box));
        foreach (var slot in _product.Slots.Take(2))
            Stock(PackScene, slot);

        var back = await Poll(() => origin.Cards.Contains(box));
        await Frames(LongIdleFrames);

        AssertBool(back).IsTrue();
        AssertBool(origin.Cards.Contains(box)).IsTrue();
        AssertInt(PackCount()).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CarriedPackGoesToAnotherRackSlotWhenItsOwnSlotIsTaken()
    {
        HireAndOpen();
        var origin = _rack.Slots.First();
        var pack = Stock(PackScene, origin);
        await Poll(() => IsCarried(pack));
        foreach (var slot in _product.Slots)
            Stock(PackScene, slot);
        var squatter = Stock(PackScene, origin);

        var back = await Poll(() => _rack.Slots.Any(slot => slot.Cards.Contains(pack)));

        AssertBool(back).IsTrue();
        AssertBool(origin.Cards.Contains(squatter)).IsTrue();
        AssertBool(origin.Cards.Contains(pack)).IsFalse();
        AssertInt(PackCount()).IsEqual(ProductSlots + 2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TargetSlotFilledEnRouteSendsThePackToTheRemainingSlot()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());
        await Poll(() => IsCarried(pack));
        foreach (var slot in _product.Slots.Take(ProductSlots - 1))
            Stock(PackScene, slot);

        var last = _product.Slots.Last();
        var placed = await Poll(() => last.Cards.Contains(pack));

        AssertBool(placed).IsTrue();
        AssertInt(PackCount()).IsEqual(ProductSlots);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SourceFreedEnRouteIsSkippedAndTheNextPackIsShelved()
    {
        HireAndOpen();
        var (near, far) = await TwoRackPacksWhileWalkingToTheNearOne();

        near.Free();
        var placed = await Poll(() => _product.Slots.Any(slot => slot.Cards.Contains(far)));

        AssertBool(placed).IsTrue();
        AssertInt(PackCount()).IsEqual(1);
        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SourceTakenByThePlayerEnRouteIsLeftAlone()
    {
        HireAndOpen();
        var (near, far) = await TwoRackPacksWhileWalkingToTheNearOne();
        var camera = new Camera3D();
        _shop.AddChild(camera);
        // What ContainerHolder.TryPickUp does: off every layer, frozen, then under the camera.
        near.CollisionLayer = 0;
        near.CollisionMask = 0;
        near.Freeze = true;
        near.Reparent(camera);

        var placed = await Poll(() => _product.Slots.Any(slot => slot.Cards.Contains(far)));

        AssertBool(placed).IsTrue();
        AssertThat(near.GetParent()).IsEqual(camera);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task NoRackAndNoProductShelfLeavesTheRestockerIdleAtHome()
    {
        _rack.Free();
        _product.Free();
        HireAndOpen();
        var home = _restocker.GlobalPosition;

        await Frames(IdleFrames);

        AssertThat(_restocker.Phase).IsEqual(RestockerPhase.Idle);
        AssertThat(_restocker.GlobalPosition).IsEqual(home);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PausingWhileCarryingFreezesTheWalkAndUnpausingFinishesIt()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());
        await Poll(() => IsCarried(pack));
        var at = _restocker.GlobalPosition;
        _tree.Paused = true;

        await Frames(IdleFrames);

        AssertThat(_restocker.GlobalPosition).IsEqual(at);
        AssertBool(IsCarried(pack)).IsTrue();
        _tree.Paused = false;
        var placed = await Poll(() => _product.Slots.Any(slot => slot.Cards.Contains(pack)));
        AssertBool(placed).IsTrue();
        AssertInt(PackCount()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ClosingTheShopWhileCarryingHoldsThePackUntilItReopens()
    {
        HireAndOpen();
        var pack = Stock(PackScene, _rack.Slots.First());
        await Poll(() => IsCarried(pack));
        _day.CloseShop();

        await Frames(IdleFrames);

        AssertBool(IsCarried(pack)).IsTrue();
        _day.OpenShop();
        var placed = await Poll(() => _product.Slots.Any(slot => slot.Cards.Contains(pack)));
        AssertBool(placed).IsTrue();
        AssertInt(PackCount()).IsEqual(1);
    }

}
