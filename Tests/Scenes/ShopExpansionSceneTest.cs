using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The first expansion lot through the real shop scene: buying it opens the wall and the navigation, raises the
///     customer count, survives a save, and lets furniture stand on the lot.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopExpansionSceneTest
{
    private const int LotPrice = 1500;
    private const int ExtraCustomers = 6;
    private const int SettleRounds = 3;

    private InMemorySlotFileSystem _files = null!;
    private ShopExpansionRig _rig = null!;
    private ShopExpansionRig? _second;

    [BeforeTest]
    public async Task Setup()
    {
        _files = new InMemorySlotFileSystem();
        _rig = await ShopExpansionRig.StartAsync(_files);
    }

    [AfterTest]
    public void Teardown()
    {
        _second?.Close();
        _second = null;
        _rig.Close();
        ShopPauseRig.Teardown();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task OpensTheLotAndMakesItReachable()
    {
        _rig.MeetTheLotRequirements();
        AssertBool(_rig.Reaches(ShopExpansionRig.ShelfArea, ShopExpansionRig.LotStorefrontPart)).IsFalse();

        AssertThat(await _rig.BuyAsync()).IsEqual(PurchaseOutcome.Bought);

        AssertBool(_rig.Reaches(ShopExpansionRig.ShelfArea, ShopExpansionRig.LotStorefrontPart)).IsTrue();
        AssertBool(_rig.Reaches(ShopExpansionRig.StorageInside, ShopExpansionRig.LotStoragePart)).IsTrue();
        AssertBool(_rig.View.IsOpen).IsTrue();
        AssertBool(ShapesOf("World/Walls/LeftWall").All(shape => shape.Disabled)).IsTrue();
        // The rest of the walls stand: the back wall still wraps the storage behind the storefront.
        foreach (var wall in new[] { "BackWall", "LotBack", "LotOuterLeft" })
            AssertBool(ShapesOf($"World/Walls/{wall}").Any(shape => shape.Disabled)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task LockedLotStaysClosed()
    {
        // The money is there but the shop level is not.
        _rig.Money.Add(ShopExpansionRig.LotFunds);
        int balance = _rig.Money.Balance;

        AssertThat(await _rig.BuyAsync()).IsEqual(PurchaseOutcome.Locked);

        AssertInt(_rig.Money.Balance).IsEqual(balance);
        AssertBool(_rig.View.IsOpen).IsFalse();
        AssertBool(_rig.Reaches(ShopExpansionRig.ShelfArea, ShopExpansionRig.LotStorefrontPart)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SpawnerCountRises()
    {
        _rig.MeetTheLotRequirements();
        int before = _rig.Spawner.EffectiveCustomersPerDay;
        AssertInt(before).IsEqual(_rig.Spawner.CustomersPerDay);

        await _rig.BuyAsync();

        AssertInt(_rig.Spawner.EffectiveCustomersPerDay).IsEqual(_rig.Spawner.CustomersPerDay + ExtraCustomers);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task SaveRoundTrip()
    {
        _rig.MeetTheLotRequirements();
        await _rig.BuyAsync();
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        // A starting shop loads its active slot by itself (AutoLoadOnStart), so no explicit load is needed.
        for (var i = 0; i < SettleRounds; i++)
            await ShopExpansionRig.SettleAsync();

        AssertBool(_second.View.IsOpen).IsTrue();
        AssertBool(_second.Reaches(ShopExpansionRig.ShelfArea, ShopExpansionRig.LotStorefrontPart)).IsTrue();
        AssertInt(_second.Spawner.EffectiveCustomersPerDay).IsEqual(_second.Spawner.CustomersPerDay + ExtraCustomers);
        var rent = new ExpansionRentBillSource(_second.Expansion).GetBillLines(1).ToList();
        AssertBool(rent.Count > 0 && rent.All(line => line.Amount > 0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UnknownSavedLotIsIgnored()
    {
        var save = new NodeSave();
        save.SetOrAddProperty("owned", "[\"side_wing_1\",\"no_such_lot\"]");

        _rig.Expansion.Load(save);

        AssertBool(_rig.Expansion.Plan.Owned.SetEquals([ShopExpansionRig.LotId])).IsTrue();
        AssertBool(_rig.View.IsOpen).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BuyingTwiceIsRefused()
    {
        _rig.MeetTheLotRequirements();
        await _rig.BuyAsync();
        int balance = _rig.Money.Balance;

        AssertThat(await _rig.BuyAsync()).IsEqual(PurchaseOutcome.AlreadyOwned);

        AssertInt(_rig.Money.Balance).IsEqual(balance);
        AssertInt(_rig.Money.Balance).IsEqual(500 + ShopExpansionRig.LotFunds - LotPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task FurnitureCanStandOnTheLot()
    {
        _rig.MeetTheLotRequirements();
        _rig.Controller.Begin(_rig.NewBox());
        _rig.Controller.AimAt(-4f, 6f);
        AssertBool(_rig.Controller.CurrentValid).IsFalse();

        await _rig.BuyAsync();
        _rig.Controller.AimAt(-4f, 6f);
        await ShopExpansionRig.SettleAsync();

        AssertBool(_rig.Controller.CurrentValid).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task LotFurnitureSurvivesReload()
    {
        _rig.MeetTheLotRequirements();
        await _rig.BuyAsync();
        await _rig.PlaceShelfAsync(-4f, 6f);
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        _second.Saves.LoadGame();
        await ShopExpansionRig.SettleAsync();

        AssertInt(_second.FurnitureRoot.GetChildCount()).IsEqual(1);
        var restored = (PlacedFurniture)_second.FurnitureRoot.GetChild(0);
        AssertBool(restored.Pose.X == -4f && restored.Pose.Z == 6f).IsTrue();
    }

    private System.Collections.Generic.IEnumerable<CollisionShape3D> ShapesOf(string wallPath) =>
        _rig.Shop.GetNode(wallPath).GetChildren().OfType<CollisionShape3D>();
}
