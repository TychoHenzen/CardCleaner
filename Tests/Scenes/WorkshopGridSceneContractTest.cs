using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Workshop.Components;
using CardCleaner.Scripts.Features.Workshop.Models;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Pins the workshop grid scene: 0.2 m cells, the baked walls are exactly the layout spec, the floor and
/// ceiling slabs cover the whole 40 m square, nearly all of the square is usable floor, every room and hallway
/// is reachable through 3 m openings, the north hallway forks into both northern rooms, and no runtime-only
/// fields are serialized into the scene.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WorkshopGridSceneContractTest
{
    private const string GridScenePath = "res://Scenes/Workshop/WorkshopGrid.tscn";
    private const float ExpectedCellSize = 0.2f;
    private const float GrayTolerance = 0.06f;
    private const float SquareSide = 40f;
    private const float SlabTolerance = 0.001f;
    private const float MinimumFloorShare = 0.9f;

    // A clear cell needs 7 floor cells on every side: a 15-cell (3 m) square, so routes only pass 3 m openings.
    private const int ThreeMetreClearanceCells = 7;
    private const int ArmClearanceCells = 4;
    private const int ArmLengthCells = 15;
    private const int MinimumRouteTurns = 2;
    private const int ForkArms = 3;

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
    public void BakedCellsAreExactlyTheLayoutWalls()
    {
        var used = _grid.GetUsedCells();
        var baked = used.Select(c => new Vector2I(c.X, c.Z)).ToHashSet();

        AssertBool(used.All(c => c.Y == WorkshopHallLayout.WallLayer)).IsTrue();
        AssertBool(used.All(c => _grid.GetCellItem(c) == WorkshopHallLayout.WallItem)).IsTrue();
        AssertBool(baked.SetEquals(WorkshopHallLayout.Default.Walls)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WallItemHasAGrayMeshAndACollisionShape()
    {
        var library = _grid.MeshLibrary;
        var mesh = library.GetItemMesh(WorkshopHallLayout.WallItem);
        var color = ((StandardMaterial3D)mesh.SurfaceGetMaterial(0)).AlbedoColor;

        AssertThat(library.GetItemShapes(WorkshopHallLayout.WallItem)[0].As<Shape3D>()).IsNotNull();
        AssertBool(Mathf.Abs(color.R - color.G) < GrayTolerance && Mathf.Abs(color.G - color.B) < GrayTolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FloorAndCeilingSlabsCoverTheWholeSquare()
    {
        foreach (var slab in new[] { "Floor", "Ceiling" })
        {
            var shape = (BoxShape3D)_grid.GetNode<CollisionShape3D>($"{slab}/CollisionShape3D").Shape;
            var centre = _grid.GetNode<Node3D>(slab).Position;

            AssertBool(Mathf.Abs(shape.Size.X - SquareSide) < SlabTolerance && Mathf.Abs(shape.Size.Z - SquareSide) < SlabTolerance).IsTrue();
            AssertBool(Mathf.Abs(centre.X) < SlabTolerance && Mathf.Abs(centre.Z) < SlabTolerance).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NearlyAllOfTheSquareIsUsableFloor()
    {
        var paths = new WorkshopGridPaths(_grid, 0);
        var share = paths.Floor.Count / (float)WorkshopHallLayout.AllCells.Count();

        GD.Print($"[workshop-grid] floor share {share:P1}");
        AssertBool(share >= MinimumFloorShare).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SectionMarkersMatchTheLayoutSections()
    {
        var markers = _grid.GetNode("Sections").GetChildren().OfType<Marker3D>().ToList();

        AssertThat(markers.Count).IsEqual(WorkshopHallLayout.Sections.Count);
        foreach (var section in WorkshopHallLayout.Sections)
        {
            var marker = _grid.GetNode<Marker3D>($"Sections/{section.Name}");
            AssertThat(new Vector2(marker.Position.X, marker.Position.Z)).IsEqual(section.Anchor);
            AssertThat(marker.GetNodeOrNull<Label3D>("Sign")).IsNotNull();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EverySectionIsReachableFromTheCabinetRoomThroughThreeMetreOpenings()
    {
        var paths = new WorkshopGridPaths(_grid, ThreeMetreClearanceCells);
        var reachable = paths.ReachableFrom(SectionCell(paths, "CabinetRoom"));

        foreach (var section in WorkshopHallLayout.Sections)
            AssertBool(reachable.Contains(SectionCell(paths, section.Name))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NorthHallwayForksIntoBothNorthernRooms()
    {
        var paths = new WorkshopGridPaths(_grid, ArmClearanceCells);
        var fork = paths.CellOfLocal(new Vector3(0f, 0f, -16.4f));

        // West into the office, east into build room A, south back to the cabinet room.
        AssertBool(Directions.Count(direction => IsArm(paths.Clear, fork, direction)) >= ForkArms).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RouteFromTheCabinetRoomToTheOrderOfficeTurnsSeveralTimes()
    {
        var paths = new WorkshopGridPaths(_grid, ArmClearanceCells);

        var route = paths.StraightenPath(paths.ShortestPath(SectionCell(paths, "CabinetRoom"), SectionCell(paths, "OrderOffice")));

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

    private Vector2I SectionCell(WorkshopGridPaths paths, string name) =>
        paths.CellOfLocal(_grid.GetNode<Node3D>($"Sections/{name}").Position);

    private static bool IsArm(IReadOnlySet<Vector2I> clear, Vector2I centre, Vector2I direction)
    {
        for (var step = 1; step <= ArmLengthCells; step++)
            if (!clear.Contains(centre + direction * step))
                return false;
        return true;
    }
}
