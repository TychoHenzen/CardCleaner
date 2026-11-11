using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The restocker through a real slot save and reload of the shop scene (hired, at home, empty hands, a carried
///     container reloaded once as a loose one), a saved roster that does not hire it, and its wage and dismissal.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopRestockerSaveSceneTest : ShopStaffSaveSceneTestBase
{
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string HandName = "Hand";
    private const int IdleFrames = 120;

    [TestCase]
    [TestCategory("Unit")]
    public async Task Hired_restocker_survives_save_and_reload_at_home_with_empty_hands()
    {
        HireCashier();
        HireRestocker();
        var home = _rig.Restocker.GlobalPosition;
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        await SettleAsync();

        AssertBool(_second.Staff.IsHired(StaffDefaults.RestockerId)).IsTrue();
        AssertBool(_second.Restocker.Visible).IsTrue();
        AssertThat(_second.Restocker.GlobalPosition).IsEqual(home);
        AssertInt(_second.Restocker.GetNode(HandName).GetChildCount()).IsEqual(0);
        AssertThat(_second.Staff.HiredRoles.Select(role => role.Id).First()).IsEqual(StaffDefaults.CashierId);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Pack_carried_at_save_time_reloads_once_as_a_loose_container()
    {
        HireRestocker();
        OpenRegisteredDay();
        var pack = StockRackPack();
        var timeout = await BoundedWait.Until(
            () => pack.GetParent()?.Name.ToString() == HandName, "the restocker to carry the pack");
        if (timeout != null)
            AssertBool(false).OverrideFailureMessage(timeout).IsTrue();
        var before = PackCount();
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        await SettleAsync();

        AssertInt(PackCount()).IsEqual(before);
        AssertInt(_second.Restocker.GetNode(HandName).GetChildCount()).IsEqual(0);
        AssertBool(PackContainers().All(c => c.GetParent()?.Name.ToString() != HandName)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Saved_roster_with_an_unknown_role_or_without_the_restocker_hires_no_restocker()
    {
        var log = new RecordingLog();
        using var sink = Capture(log);
        var unknown = new NodeSave();
        unknown.SetOrAddProperty(HiredKey, "[\"cashier\",\"ghost\"]");
        var omitted = new NodeSave();
        omitted.SetOrAddProperty(HiredKey, "[\"cashier\"]");

        _rig.Staff.Load(unknown);
        AssertInt(log.Warnings.Count).IsEqual(1);
        AssertBool(_rig.Staff.IsHired(StaffDefaults.RestockerId)).IsFalse();
        _rig.Staff.Load(omitted);

        AssertInt(log.Warnings.Count).IsEqual(1);
        AssertBool(_rig.Staff.IsHired(StaffDefaults.RestockerId)).IsFalse();
        AssertBool(_rig.Staff.IsHired(StaffDefaults.CashierId)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Both_wages_are_charged_once_and_a_dismissed_restocker_stops_working()
    {
        HireCashier();
        HireRestocker();
        var day = OpenRegisteredDay();
        AssertThat(_rig.Staff.Dismiss(StaffDefaults.RestockerId)).IsEqual(StaffResult.Dismissed);
        var pack = StockRackPack();

        for (var i = 0; i < IdleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;

        AssertBool(_rig.Restocker.Visible).IsFalse();
        AssertBool(RackSlot().Cards.Contains(pack)).IsTrue();
        _rig.Money.Add(StaffDefaults.RestockerHireFee);
        AssertThat(_rig.Staff.Hire(StaffDefaults.RestockerId)).IsEqual(StaffResult.Hired);
        DayReport? report = null;
        day.Closed += closed => report = closed;
        day.CloseShop();
        AssertBool(report != null).IsTrue();
        AssertInt(report!.Settlement.Lines.Count(line => line.Name == "Cashier wage")).IsEqual(1);
        AssertInt(report.Settlement.Lines.Count(line => line.Name == "Restocker wage")).IsEqual(1);
    }

    private DeckSlot RackSlot() => _rig.Shop.GetNode<CardShelf>("World/RightStorage/StorageRack").Slots.First();

    // A new pack locked into the first slot of the storage rack.
    private CardContainer StockRackPack()
    {
        var pack = GD.Load<PackedScene>(PackScene).Instantiate<CardContainer>();
        _rig.Shop.GetNode("World").AddChild(pack);
        AssertBool(RackSlot().TryLock(pack)).IsTrue();
        return pack;
    }

    private IEnumerable<CardContainer> PackContainers() =>
        _second!.Shop.GetTree().GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(c => c.Kind == CardContainerKind.Pack && !c.IsOpened);

    private int PackCount() => (_second?.Shop ?? _rig.Shop).GetTree().GetNodesInGroup("card_containers")
        .OfType<CardContainer>().Count(c => c.Kind == CardContainerKind.Pack && !c.IsOpened);
}
