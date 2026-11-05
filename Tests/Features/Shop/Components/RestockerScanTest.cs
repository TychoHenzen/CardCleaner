using System.Linq;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     What the restocker sees: racked and loose containers are sources, held, opened and carried ones are not, and
///     only empty slots of sales shelves are targets.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RestockerScanTest
{
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string RackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const float Radius = 4f;

    private CardShelf _rack = null!;
    private CardShelf _product = null!;
    private RestockerScan _scan = null!;
    private Node3D _world = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = AddNode(new Node3D());
        _rack = Shelf(RackScene, new Vector3(0f, 0f, 0f));
        _product = Shelf(ProductShelfScene, new Vector3(30f, 0f, 0f));
        _scan = new RestockerScan((SceneTree)Engine.GetMainLoop(), Radius);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PackOnARackSlotIsARackSource()
    {
        var pack = Container(PackScene, new Vector3(0f, 1f, 0f));
        AssertBool(_rack.Slots.First().TryLock(pack)).IsTrue();

        var result = _scan.Gather(Vector3.Zero);

        AssertInt(result.Sources.Count).IsEqual(1);
        AssertBool(result.Sources[0].OnRack).IsTrue();
        AssertThat(result.Sources[0].Kind).IsEqual(StockKind.Pack);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LoosePackNearARackIsAFloorSourceAndAFarOneIsSkipped()
    {
        Container(PackScene, new Vector3(2f, 1f, 0f));
        Container(PackScene, new Vector3(-50f, 1f, 0f));

        var result = _scan.Gather(Vector3.Zero);

        AssertInt(result.Sources.Count).IsEqual(1);
        AssertBool(result.Sources[0].OnRack).IsFalse();
        AssertFloat(result.Sources[0].DistanceFromRestocker).IsEqualApprox(2.236f, 0.01f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpenedHeldAndCarriedContainersAreSkipped()
    {
        var opened = Container(PackScene, new Vector3(1f, 1f, 0f));
        AssertBool(opened.Open()).IsTrue();
        var camera = AddNode(new Camera3D());
        var held = Container(PackScene, new Vector3(1f, 1f, 1f));
        held.Reparent(camera);
        var hand = AddNode(new Node3D { Name = "Hand" });
        var carried = Container(PackScene, new Vector3(1f, 1f, 2f));
        carried.Reparent(hand);

        var result = _scan.Gather(Vector3.Zero);

        // The opened pack spawns its boosters over the next frames, so none exist yet when the scan runs.
        AssertBool(opened.IsOpened).IsTrue();
        AssertInt(result.Sources.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OnlyEmptySalesShelfSlotsAreFreeTargets()
    {
        var filled = Container(PackScene, new Vector3(30f, 1f, 0f));
        AssertBool(_product.Slots.First().TryLock(filled)).IsTrue();

        var result = _scan.Gather(Vector3.Zero);

        AssertInt(result.Slots.Count).IsEqual(_product.SlotCount - 1);
        AssertBool(result.Slots.All(s => s.Shelf == ShelfKind.ProductShelf)).IsTrue();
        AssertBool(result.Slots.Any(s => s.ShelfId < 0)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void IdsFollowTreeOrder()
    {
        var first = Container(BoxScene, new Vector3(1f, 1f, 0f));
        var second = Container(PackScene, new Vector3(-1f, 1f, 0f));

        var result = _scan.Gather(Vector3.Zero);

        AssertThat(result.SourceNode(0)).IsEqual(first);
        AssertThat(result.SourceNode(1)).IsEqual(second);
        AssertThat(result.Sources.Select(s => s.Id).ToArray()).IsEqual(new[] { 0, 1 });
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AContainerKeepsItsIdWhenAnEarlierOneLeaves()
    {
        var first = Container(BoxScene, new Vector3(1f, 1f, 0f));
        var second = Container(PackScene, new Vector3(-1f, 1f, 0f));
        var before = _scan.Gather(Vector3.Zero);
        var secondId = before.Sources.Single(s => s.Kind == StockKind.Pack).Id;

        first.Free();
        var after = _scan.Gather(Vector3.Zero);

        AssertThat(after.Sources.Select(s => s.Id).ToArray()).IsEqual(new[] { secondId });
        AssertThat(after.SourceNode(secondId)).IsEqual(second);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CarriedContainerIsTheOnlySourceAndRackSlotsListTheEmptyOnes()
    {
        var pack = Container(PackScene, new Vector3(1f, 1f, 0f));
        Container(PackScene, new Vector3(-1f, 1f, 0f));
        AssertBool(_rack.Slots.First().TryLock(pack)).IsTrue();
        var origin = _rack.Slots.First();
        origin.ReleaseCard(pack);

        var result = _scan.Gather(Vector3.Zero, pack);

        AssertInt(result.Sources.Count).IsEqual(1);
        AssertThat(result.SourceNode(result.Sources[0].Id)).IsEqual(pack);
        AssertInt(result.RackSlots.Count).IsEqual(_rack.SlotCount);
        AssertThat(result.PutBackSlot(pack, origin, Vector3.Zero)).IsEqual(origin);
    }

    private CardShelf Shelf(string path, Vector3 at)
    {
        var shelf = AddNode(GD.Load<PackedScene>(path).Instantiate<CardShelf>());
        shelf.GlobalPosition = at;
        return shelf;
    }

    private CardContainer Container(string path, Vector3 at)
    {
        var container = GD.Load<PackedScene>(path).Instantiate<CardContainer>();
        _world.AddChild(container);
        container.GlobalPosition = at;
        return container;
    }
}
