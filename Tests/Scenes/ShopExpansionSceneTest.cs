using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Expansion;
using CardCleaner.Scripts.Features.Shop.Furniture;
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

    private static readonly System.Text.RegularExpressions.Regex DoorName =
        new("door|portal|gate", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

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
    public async Task OpensTheLotAndMakesItReachable()
    {
        _rig.MeetTheLotRequirements();
        AssertBool(_rig.Reaches(ShopExpansionRig.ShelfArea, ShopExpansionRig.LotStorefrontPart)).IsFalse();
        AssertBool(_rig.Reaches(ShopExpansionRig.StorageInside, ShopExpansionRig.LotStoragePart)).IsFalse();
        var doorsBefore = DoorLikePaths();

        AssertThat(await _rig.BuyAsync()).IsEqual(PurchaseOutcome.Bought);

        // The storefront and the lot are one room, and the storage room reaches into the lot's storage part.
        AssertBool(_rig.Reaches(ShopExpansionRig.ShelfArea, ShopExpansionRig.LotStorefrontPart)).IsTrue();
        AssertBool(_rig.Reaches(ShopExpansionRig.StorageInside, ShopExpansionRig.LotStoragePart)).IsTrue();
        AssertBool(_rig.View.IsOpen).IsTrue();
        var sealing = ShapesOf("World/Walls/LeftWall").ToList();
        AssertBool(sealing.Count > 0).IsTrue();
        AssertBool(sealing.All(shape => shape.Disabled)).IsTrue();
        AssertBool(_rig.Shop.GetNode<Node3D>("World/Walls/LeftWall").Visible).IsFalse();
        // The rest of the walls stand: the back wall still wraps the storage behind the storefront.
        foreach (var wall in new[] { "BackWall", "LotBack", "LotOuterLeft" })
        {
            var shapes = ShapesOf($"World/Walls/{wall}").ToList();
            AssertBool(shapes.Count > 0).IsTrue();
            AssertBool(shapes.Any(shape => shape.Disabled)).IsFalse();
        }

        // No street door is added: the purchase adds no door node, and none sits at the lot's walls.
        AssertBool(DoorLikePaths().SetEquals(doorsBefore)).IsTrue();
        AssertBool(DoorLikePaths().Any(path => path.Contains("/LotBack") || path.Contains("/LotOuterLeft"))).IsFalse();
    }

    [TestCase]
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
    public async Task SpawnerCountRises()
    {
        _rig.MeetTheLotRequirements();
        int before = _rig.Spawner.EffectiveCustomersPerDay;
        AssertInt(before).IsEqual(_rig.Spawner.CustomersPerDay);

        await _rig.BuyAsync();

        AssertInt(_rig.Spawner.EffectiveCustomersPerDay).IsEqual(_rig.Spawner.CustomersPerDay + ExtraCustomers);
    }

    [TestCase]
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
    public void UnknownSavedLotIsIgnored()
    {
        var save = new NodeSave();
        save.SetOrAddProperty("owned", "[\"side_wing_1\",\"no_such_lot\"]");

        _rig.Expansion.Load(save);

        AssertBool(_rig.Expansion.Plan.Owned.SetEquals([ShopExpansionRig.LotId])).IsTrue();
        AssertBool(_rig.View.IsOpen).IsTrue();
    }

    [TestCase]
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
    public async Task LotFurnitureSurvivesReload()
    {
        _rig.MeetTheLotRequirements();
        await _rig.BuyAsync();
        // Counted before the lot shelf, because the second shop loads the save as it starts.
        var standing = _rig.FurnitureRoot.GetChildCount();
        await _rig.PlaceShelfAsync(-4f, 6f);
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        _second.Saves.LoadGame();
        await ShopExpansionRig.SettleAsync();

        // The starting furniture comes back beside the shelf on the lot.
        AssertInt(_second.FurnitureRoot.GetChildCount()).IsEqual(standing + 1);
        AssertInt(_second.FurnitureRoot.GetChildren().OfType<PlacedFurniture>()
            .Count(placed => placed.Pose.X == -4f && placed.Pose.Z == 6f)).IsEqual(1);
    }

    // Every node of the shop scene that is named like a door, doorway or portal.
    private System.Collections.Generic.HashSet<string> DoorLikePaths() =>
        _rig.Shop.FindChildren("*", "", true, false)
            .Where(node => DoorName.IsMatch(node.Name.ToString()))
            .Select(node => node.GetPath().ToString())
            .ToHashSet();

    private System.Collections.Generic.IEnumerable<CollisionShape3D> ShapesOf(string wallPath) =>
        _rig.Shop.GetNode(wallPath).GetChildren().OfType<CollisionShape3D>();
}
