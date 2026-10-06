using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Workshop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Pins the workshop grid scene: 0.2 m cells, gray floor and wall items that each carry a mesh and a collision
/// shape, a non-empty authored layout of several rooms with winding hallways and a fork, and no
/// runtime-only fields serialized into the scene.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WorkshopGridSceneContractTest
{
    private const string GridScenePath = "res://Scenes/Workshop/WorkshopGrid.tscn";
    private const float ExpectedCellSize = 0.2f;
    private const float GrayTolerance = 0.06f;
    private const int MinimumRooms = 5;
    private const int ArmClearanceCells = 4;
    private const int ArmLengthCells = 15;
    private const int MinimumForkArms = 3;
    private const int TurnArms = 2;
    private const int MinimumRouteTurns = 4;
    private const int MaximumCells = 40000;

    private static readonly Vector2I[] Directions = [Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up];

    private PackedScene _packed = null!;
    private WorkshopGrid _grid = null!;

    [BeforeTest]
    public void Setup()
    {
        _packed = GD.Load<PackedScene>(GridScenePath);
        _grid = _packed.Instantiate<WorkshopGrid>();
    }

    [AfterTest]
    public void Teardown()
    {
        _grid.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CellsAreTwentyCentimetresOnEveryAxis()
    {
        AssertThat(_grid.CellSize).IsEqual(new Vector3(ExpectedCellSize, ExpectedCellSize, ExpectedCellSize));
        AssertThat(_grid.Coordinates.CellSize).IsEqual(_grid.CellSize);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LayoutHoldsFloorAndWallCellsWithinTheCellBudget()
    {
        AssertBool(_grid.GetUsedCellsByItem(WorkshopGridPaths.FloorItem).Count > 0).IsTrue();
        AssertBool(_grid.GetUsedCellsByItem(WorkshopGridPaths.WallItem).Count > 0).IsTrue();
        AssertBool(_grid.GetUsedCells().Count < MaximumCells).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FloorsLieOneLayerBelowTheWallsSoTheirTopIsGroundLevel()
    {
        AssertBool(_grid.GetUsedCellsByItem(WorkshopGridPaths.FloorItem).All(c => c.Y == WorkshopGridPaths.FloorLayer)).IsTrue();
        AssertBool(_grid.GetUsedCellsByItem(WorkshopGridPaths.WallItem).All(c => c.Y == 0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FloorAndWallItemsEachHaveAMeshAndACollisionShape()
    {
        var library = _grid.MeshLibrary;

        AssertThat(library).IsNotNull();
        foreach (var item in new[] { WorkshopGridPaths.FloorItem, WorkshopGridPaths.WallItem })
        {
            AssertThat(library.GetItemMesh(item)).IsNotNull();
            AssertThat(library.GetItemShapes(item).Count).IsEqual(2);
            AssertThat(library.GetItemShapes(item)[0].As<Shape3D>()).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FloorAndWallMeshesAreGray()
    {
        foreach (var item in new[] { WorkshopGridPaths.FloorItem, WorkshopGridPaths.WallItem })
        {
            var material = (StandardMaterial3D)_grid.MeshLibrary.GetItemMesh(item).SurfaceGetMaterial(0);
            var color = material.AlbedoColor;

            AssertBool(Mathf.Abs(color.R - color.G) < GrayTolerance && Mathf.Abs(color.G - color.B) < GrayTolerance).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LayoutHasSeveralRoomsOnClearFloor()
    {
        var paths = new WorkshopGridPaths(_grid, ArmClearanceCells);
        var rooms = _grid.GetNode("Rooms").GetChildren().OfType<Marker3D>().ToList();

        AssertBool(rooms.Count >= MinimumRooms).IsTrue();
        foreach (var room in rooms)
            AssertBool(paths.Clear.Contains(paths.CellOfLocal(room.Position))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HallwaysForkAtTheCrossingAndTurnAtTheCorners()
    {
        var paths = new WorkshopGridPaths(_grid, ArmClearanceCells);

        AssertBool(ArmCount(paths, "SpineCrossing") >= MinimumForkArms).IsTrue();
        AssertThat(ArmCount(paths, "NorthTurn")).IsEqual(TurnArms);
        AssertThat(ArmCount(paths, "SouthTurn")).IsEqual(TurnArms);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RouteToTheFarthestRoomWindsThroughSeveralTurns()
    {
        var paths = new WorkshopGridPaths(_grid, ArmClearanceCells);
        var from = paths.CellOfLocal(_grid.GetNode<Node3D>("Doorway").Position);
        var to = paths.CellOfLocal(_grid.GetNode<Node3D>("Rooms/FarNorth").Position);

        var route = paths.StraightenPath(paths.ShortestPath(from, to));

        AssertBool(route.Count - 1 >= MinimumRouteTurns).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SceneDoesNotSerializeRuntimeOnlyFields()
    {
        var state = _packed.GetState();

        for (var node = 0; node < state.GetNodeCount(); node++)
        for (var i = 0; i < state.GetNodePropertyCount(node); i++)
            AssertBool(state.GetNodePropertyName(node, i).ToString().StartsWith('_')).IsFalse();
    }

    private int ArmCount(WorkshopGridPaths paths, string junction)
    {
        var centre = paths.CellOfLocal(_grid.GetNode<Node3D>($"Junctions/{junction}").Position);
        return Directions.Count(direction => IsArm(paths.Clear, centre, direction));
    }

    private static bool IsArm(IReadOnlySet<Vector2I> clear, Vector2I centre, Vector2I direction)
    {
        for (var step = 1; step <= ArmLengthCells; step++)
            if (!clear.Contains(centre + direction * step))
                return false;
        return true;
    }
}
