using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Expansion;
using CardCleaner.Scripts.Features.Shop.Furniture;
using CardCleaner.Scripts.Features.Shop.Ordering;
using CardCleaner.Tests.TestUtilities;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Shop.Furniture;

/// <summary>Save and load of the shop's placed shelves and unopened boxes, without the shop scene.</summary>
[TestSuite]
[RequireGodotRuntime]
public class FurnitureSaveServiceTest
{
    private const string CatalogPath = "res://Data/Shop/Furniture/FurnitureItems.tres";
    private const string OrderCatalogPath = "res://Data/Shop/FurnitureOrderCatalog.tres";
    private const string BoxScenePath = "res://Scenes/Shop/Items/ShelfBox.tscn";
    private const float LotMinX = -8f;
    private const float LotX = -4f;

    private Window _root = null!;
    private Node3D _parent = null!;
    private Node3D _furnitureRoot = null!;
    private Node3D _boxRoot = null!;
    private FurnitureSaveService _service = null!;
    private DeliveryMarker _delivery = null!;

    [BeforeTest]
    public void Setup()
    {
        _parent = new Node3D();
        _furnitureRoot = new Node3D { Name = "Furniture" };
        _boxRoot = new Node3D { Name = "Boxes" };
        _parent.AddChild(_furnitureRoot);
        _parent.AddChild(_boxRoot);
        _delivery = new DeliveryMarker { Name = "Delivery" };
        _parent.AddChild(_delivery);
        _service = new FurnitureSaveService
        {
            DeliveryPoint = _delivery,
            Furniture = GD.Load<FurnitureCatalog>(CatalogPath),
            Boxes = GD.Load<OrderCatalog>(OrderCatalogPath),
            FurnitureRoot = _furnitureRoot,
            BoxRoot = _boxRoot,
        };
        _parent.AddChild(_service);
        AddNode(_parent);
        _root = _parent.GetTree().Root;
        UseExpansion(new FakeExpansion());
    }

    private void UseExpansion(FakeExpansion? expansion)
    {
        TestServices.Reset();
        if (expansion != null)
            TestServices.Container.RegisterSingleton<IShopExpansion>(expansion);
        TestServices.Inject(_service);
    }

    [AfterTest]
    public void Teardown()
    {
        // Freeing the parent frees the service and both roots with it.
        _root.RemoveChild(_parent);
        _parent.Free();
    }

    [TestCase]
    public void TwoShelvesRoundTripWithIdenticalPoses()
    {
        Place(8f, 6f, 0);
        Place(4f, 3f, 1);
        var save = new NodeSave();
        _service.Save(save);
        ClearPlaced();

        _service.Load(save);

        var poses = PlacedPoses();
        AssertInt(poses.Count).IsEqual(2);
        AssertBool(poses.Any(p => p.X == 8f && p.Z == 6f && p.QuarterTurns == 0)).IsTrue();
        AssertBool(poses.Any(p => p.X == 4f && p.Z == 3f && p.QuarterTurns == 1)).IsTrue();
    }

    [TestCase]
    public void AnUnknownIdIsSkippedAndTheRestLoad()
    {
        var sink = new RecordingLog();
        var previous = ILog.Sink;
        try
        {
            ILog.Sink = sink;
            var save = SaveWith(
                new FurnitureRecord("nope", 8f, 6f, 0, false),
                new FurnitureRecord("shelf_unit", 4f, 3f, 0, false));

            _service.Load(save);

            AssertInt(PlacedPoses().Count).IsEqual(1);
            AssertInt(sink.Errors.Count).IsEqual(1);
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    // A piece whose saved pose no longer fits (its item grew in an update, or the pose was never valid) is not lost:
    // it comes back packed in its furniture box on the delivery area, so the player can place it again.
    [TestCase]
    public void APieceThatNoLongerFitsComesBackAsItsBoxAtTheDeliveryArea()
    {
        var save = SaveWith(
            new FurnitureRecord("shelf_unit", 0.5f, 0.5f, 0, false),
            new FurnitureRecord("shelf_unit", 8f, 6f, 0, false));

        _service.Load(save);

        var poses = PlacedPoses();
        AssertInt(poses.Count).IsEqual(1);
        AssertBool(poses[0].X == 8f && poses[0].Z == 6f).IsTrue();
        AssertBoxOnDeliveryArea("shelf_unit");
    }

    [TestCase]
    public void ARecordOnAnOwnedLotLoads()
    {
        UseExpansion(new FakeExpansion(owned: true));

        _service.Load(SaveWith(new FurnitureRecord("shelf_unit", LotX, 6f, 0, false)));

        AssertInt(PlacedPoses().Count).IsEqual(1);
    }

    // The expansion's own save entry must load before the furniture's; with the order reversed the lot is not
    // owned yet, so the record on it does not fit: an error names it, and the piece comes back as its box.
    [TestCase]
    public void ARecordOnAnUnownedLotComesBackAsItsBox()
    {
        var sink = new RecordingLog();
        var previous = ILog.Sink;
        try
        {
            UseExpansion(new FakeExpansion(owned: false));
            ILog.Sink = sink;

            _service.Load(SaveWith(new FurnitureRecord("shelf_unit", LotX, 6f, 0, false)));

            AssertInt(PlacedPoses().Count).IsEqual(0);
            AssertInt(sink.Errors.Count).IsEqual(1);
            AssertBoxOnDeliveryArea("shelf_unit");
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    public void AMissingExpansionServiceIsLoggedAndTheBaseFloorUsed()
    {
        var sink = new RecordingLog();
        var previous = ILog.Sink;
        try
        {
            UseExpansion(null);
            ILog.Sink = sink;

            _service.Load(SaveWith(new FurnitureRecord("shelf_unit", 8f, 6f, 0, false)));

            AssertInt(PlacedPoses().Count).IsEqual(1);
            AssertInt(sink.Errors.Count).IsEqual(1);
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    public void OverlappingRecordsKeepTheFirst()
    {
        var save = SaveWith(
            new FurnitureRecord("shelf_unit", 8f, 6f, 0, false),
            new FurnitureRecord("shelf_unit", 8f, 6f, 0, false));

        _service.Load(save);

        AssertInt(PlacedPoses().Count).IsEqual(1);
    }

    [TestCase]
    public void NonFiniteCoordinatesAreSkipped()
    {
        var sink = new RecordingLog();
        var previous = ILog.Sink;
        try
        {
            ILog.Sink = sink;
            var save = SaveWith(
                new FurnitureRecord("shelf_unit", float.NaN, 6f, 0, false),
                new FurnitureRecord("shelf_unit", 8f, 6f, 0, false));

            _service.Load(save);

            AssertInt(PlacedPoses().Count).IsEqual(1);
            AssertInt(sink.Errors.Count).IsEqual(1);
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    public void AnUnopenedBoxReturns()
    {
        var box = GD.Load<PackedScene>(BoxScenePath).Instantiate<FurnitureBox>();
        _boxRoot.AddChild(box);
        box.GlobalPosition = new Vector3(6f, 0.5f, 2f);
        var save = new NodeSave();
        _service.Save(save);
        _boxRoot.RemoveChild(box);
        box.Free();

        _service.Load(save);

        var boxes = _boxRoot.GetChildren().OfType<FurnitureBox>().ToList();
        AssertInt(boxes.Count).IsEqual(1);
        AssertString(boxes[0].Item!.Id).IsEqual("shelf_unit");
        AssertBool(Mathf.Abs(boxes[0].GlobalPosition.X - 6f) < 0.01f).IsTrue();
        AssertBool(Mathf.Abs(boxes[0].GlobalPosition.Z - 2f) < 0.01f).IsTrue();
    }

    [TestCase]
    public void ARestoredBoxCountsAsAnOccupantOfTheDeliveryArea()
    {
        // Slot 0 of the delivery grid is centred on this offset from the marker, which stands at the origin.
        var slotZero = DefaultDeliveryGrid.SlotOffset(0);
        var save = SaveWith(new FurnitureRecord("shelf_unit", slotZero.X, slotZero.Z, 0, true));
        _service.Load(save);
        var incoming = GD.Load<PackedScene>(BoxScenePath).Instantiate<Node3D>();

        var placement = _delivery.FindPlacement(
            6,
            DefaultDeliveryGrid.SlotOffset,
            new Vector2(DefaultDeliveryGrid.CellWidth, DefaultDeliveryGrid.CellDepth),
            incoming);
        incoming.Free();

        AssertBool(placement.HasValue).IsTrue();
        AssertBool(placement!.Value.Slot == 0 && placement.Value.Lift == 0f).IsFalse();
    }

    [TestCase]
    public void LoadReplacesInsteadOfAdding()
    {
        Place(8f, 6f, 0);
        var save = new NodeSave();
        _service.Save(save);

        _service.Load(save);
        _service.Load(save);

        AssertInt(PlacedPoses().Count).IsEqual(1);
    }

    [TestCase]
    public void ASaveWithoutTheKeyLoadsNothing()
    {
        _service.Load(new NodeSave());

        AssertInt(PlacedPoses().Count).IsEqual(0);
        AssertInt(_boxRoot.GetChildren().OfType<FurnitureBox>().Count()).IsEqual(0);
    }

    [TestCase]
    public void EveryPieceKeepsItsNameThroughASaveAndLoad()
    {
        var first = Place(4f, 3f, 0);
        Place(8f, 3f, 0);
        _furnitureRoot.RemoveChild(first);
        first.Free();
        Place(12f, 3f, 0);
        Place(8f, 8f, 0, "StarterShelf");
        var names = PlacedNames();
        var save = new NodeSave();
        _service.Save(save);
        ClearPlaced();

        _service.Load(save);

        AssertThat(string.Join(",", names)).IsEqual("Furniture1,Furniture0,StarterShelf");
        AssertThat(string.Join(",", PlacedNames())).IsEqual(string.Join(",", names));
    }

    [TestCase]
    public void ALoadKeepsTheStandingPieceItsRecordNamesAndMovesIt()
    {
        var starter = Place(8f, 6f, 0, "StarterShelf");
        var save = SaveWith(new FurnitureRecord("shelf_unit", 4f, 3f, 1, false, "StarterShelf"));

        _service.Load(save);

        // The same node, so its price tags and stock load onto it, at the saved pose.
        AssertBool(GodotObject.IsInstanceValid(starter)).IsTrue();
        AssertThat(string.Join(",", PlacedNames())).IsEqual("StarterShelf");
        AssertBool(starter.Pose == new FurniturePose(4f, 3f, 1)).IsTrue();
    }

    [TestCase]
    public void ALoadFreesAStandingPieceThatNoRecordKeeps()
    {
        Place(8f, 6f, 0, "StarterShelf");
        var save = SaveWith(new FurnitureRecord("shelf_unit", 4f, 3f, 0, true));

        _service.Load(save);

        // A packed starting shelf stays packed: its box comes back and the shelf does not.
        AssertInt(PlacedNames().Count).IsEqual(0);
        AssertInt(_boxRoot.GetChildren().OfType<FurnitureBox>().Count()).IsEqual(1);
    }

    [TestCase]
    public void APieceOfAnotherKindUnderTheSavedNameIsReplaced()
    {
        var item = GD.Load<FurnitureCatalog>(CatalogPath).Find("product_shelf")!;
        var other = PlacedFurniture.Create(item, new FurniturePose(8f, 6f, 0))!;
        other.StandUnder(_furnitureRoot, "StarterShelf");
        var save = SaveWith(new FurnitureRecord("shelf_unit", 8f, 6f, 0, false, "StarterShelf"));

        _service.Load(save);

        var restored = _furnitureRoot.GetChildren().OfType<PlacedFurniture>().Single();
        AssertThat(restored.Name.ToString()).IsEqual("StarterShelf");
        AssertThat(restored.Item!.Id).IsEqual("shelf_unit");
    }

    [TestCase]
    public void ASaveFromBeforeTheWholeLayoutKeepsTheStartingFurniture()
    {
        Place(8f, 6f, 0, "StarterShelf");
        var save = SaveWith(new FurnitureRecord("shelf_unit", 4f, 3f, 0, false));
        save.Properties.Remove(FurnitureSaveService.WholeLayoutKey);

        _service.Load(save);

        AssertThat(string.Join(",", PlacedNames())).IsEqual("StarterShelf,Furniture0");
    }

    [TestCase]
    public void AnOlderRecordOverlappingTheStartingFurnitureComesBackAsItsBox()
    {
        Place(8f, 6f, 0, "StarterShelf");
        var save = SaveWith(new FurnitureRecord("shelf_unit", 8f, 6f, 0, false));
        save.Properties.Remove(FurnitureSaveService.WholeLayoutKey);

        _service.Load(save);

        AssertThat(string.Join(",", PlacedNames())).IsEqual("StarterShelf");
        AssertBoxOnDeliveryArea("shelf_unit");
    }

    // Exactly one box, of the item, standing on a cell of the delivery marker's grid (the marker is at the origin).
    private void AssertBoxOnDeliveryArea(string id)
    {
        var boxes = _boxRoot.GetChildren().OfType<FurnitureBox>().ToList();
        AssertInt(boxes.Count).IsEqual(1);
        AssertString(boxes[0].Item!.Id).IsEqual(id);
        var at = boxes[0].GlobalPosition;
        AssertBool(Enumerable.Range(0, 6).Select(DefaultDeliveryGrid.SlotOffset)
                .Any(cell => Mathf.Abs(cell.X - at.X) < 0.01f && Mathf.Abs(cell.Z - at.Z) < 0.01f))
            .OverrideFailureMessage($"the box stands at {at}, on no delivery cell").IsTrue();
    }

    private static NodeSave SaveWith(params FurnitureRecord[] records)
    {
        var save = new NodeSave();
        save.SetOrAddProperty(FurnitureSaveService.RecordsKey, FurnitureSaveCodec.Encode(records));
        save.SetOrAddProperty(FurnitureSaveService.WholeLayoutKey, true);
        return save;
    }

    private PlacedFurniture Place(float x, float z, int turns, string? name = null)
    {
        var item = GD.Load<FurnitureCatalog>(CatalogPath).Find("shelf_unit")!;
        var placed = PlacedFurniture.Create(item, new FurniturePose(x, z, turns))!;
        placed.StandUnder(_furnitureRoot, name);
        return placed;
    }

    private List<string> PlacedNames() =>
        _furnitureRoot.GetChildren().OfType<PlacedFurniture>().Select(p => p.Name.ToString()).ToList();

    private void ClearPlaced()
    {
        foreach (var placed in _furnitureRoot.GetChildren().OfType<PlacedFurniture>().ToList())
        {
            _furnitureRoot.RemoveChild(placed);
            placed.Free();
        }
    }

    private List<FurniturePose> PlacedPoses() =>
        _furnitureRoot.GetChildren().OfType<PlacedFurniture>().Select(p => p.Pose).ToList();

    private sealed class FakeExpansion : IShopExpansion
    {
        public FakeExpansion(bool owned = false)
        {
            var lot = new ExpansionLot("lot_test", 1, 100, "lot_test", 10, 1, "Test lot", LotMinX);
            Plan = new ExpansionPlan([lot], owned ? new HashSet<string> { lot.Id } : new HashSet<string>());
        }

        public ExpansionPlan Plan { get; }

        public event Action<string>? Opened
        {
            add { }
            remove { }
        }

        public int ExtraCustomersPerDay => 0;

        public PurchaseOutcome TryBuy(string lotId) => PurchaseOutcome.Unknown;
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = new();

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogError(string message) => Errors.Add(message);

        public void LogError(string message, Exception exception) => Errors.Add(message);
    }
}
