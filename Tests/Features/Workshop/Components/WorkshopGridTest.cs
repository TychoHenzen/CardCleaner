using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Workshop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Workshop.Components;

/// <summary>
/// The workshop grid converts world positions to cells and back through its own transform, so it keeps
/// working when the grid is moved or turned.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WorkshopGridTest
{
    private const string GridScenePath = "res://Scenes/Workshop/WorkshopGrid.tscn";
    private const float Tolerance = 0.0001f;

    private WorkshopGrid _grid = null!;
    private Node3D _parent = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _parent = new Node3D { Position = new Vector3(60f, 0f, -4f) };
        _grid = GD.Load<PackedScene>(GridScenePath).Instantiate<WorkshopGrid>();
        _parent.AddChild(_grid);
        AddNode(_parent);
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CoordinatesUseTheGridMapCellSize()
    {
        AssertThat(_grid.Coordinates.CellSize).IsEqual(_grid.CellSize);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorldToCellAgreesWithGridMapThroughAMovedParent()
    {
        foreach (var world in new[] { new Vector3(66.1f, 0.05f, -4.0f), new Vector3(59.9f, -0.15f, -9.7f), new Vector3(85.3f, 0.5f, 3.3f) })
            AssertThat(_grid.WorldToCell(world)).IsEqual(_grid.LocalToMap(_grid.ToLocal(world)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CellToWorldRoundTripsThroughAMovedAndTurnedGrid()
    {
        _grid.Position = new Vector3(0.6f, 0f, -1.4f);
        _grid.RotationDegrees = new Vector3(0f, 90f, 0f);

        foreach (var cell in new[] { new Vector3I(30, -1, 0), new Vector3I(-4, 0, 12) })
        {
            var world = _grid.CellToWorld(cell);

            AssertThat(_grid.WorldToCell(world)).IsEqual(cell);
            AssertThat(world.IsEqualApprox(_grid.ToGlobal(_grid.MapToLocal(cell)))).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorldPositionOfABuiltCellMapsBackToThatCell()
    {
        foreach (var cell in _grid.GetUsedCells())
        {
            AssertThat(_grid.WorldToCell(_grid.CellToWorld(cell))).IsEqual(cell);
            break;
        }

        AssertBool(_grid.GetUsedCells().Count > 0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FloorCellTopSitsAtGroundLevelOfTheWorkshop()
    {
        var floorCell = new Vector3I(30, -1, 0);

        AssertThat(_grid.GetCellItem(floorCell)).IsEqual(0);
        AssertBool(Mathf.Abs(_grid.CellToWorld(floorCell).Y + _grid.CellSize.Y / 2f - _parent.Position.Y) < Tolerance).IsTrue();
    }
}
