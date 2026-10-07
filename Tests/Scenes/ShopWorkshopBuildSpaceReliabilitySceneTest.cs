using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Workshop.Components;
using CardCleaner.Scripts.Features.Workshop.Models;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Reliability of the warehouse workshop: the floor is solid at ground level everywhere, the outer wall closes
/// the square on every side, the ceiling keeps a jumping player inside, the player's capsule fits along the
/// reachable floor, and the walls keep the physics step cheap.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopBuildSpaceReliabilitySceneTest
{
    private const float GroundTolerance = 0.002f;
    private const float RayStartHeight = 1f;
    private const float CeilingMaxHeight = 4.05f;
    private const float WallProbeHeight = 1f;
    private const int ClearanceCells = 4;

    // Sampling every fourth cell on each axis keeps the ray count near 2,500 while still landing in every room.
    private const int SampleStride = 4;
    private const int QueryBatches = 5;
    private const int QueriesPerBatch = 200;
    private const double MaxQueryMicroseconds = 1000.0;
    private const int SettleFrames = 20;

    private Node3D _shop = null!;
    private Node3D _workshop = null!;
    private WorkshopGrid _grid = null!;
    private CharacterBody3D _player = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _workshop = _shop.GetNode<Node3D>("World/Workshop");
        _grid = _workshop.GetNode<WorkshopGrid>("WorkshopGrid");
        _player = _shop.GetNode<CharacterBody3D>("Player");

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FloorIsSolidAtGroundLevelAcrossTheWholeSquare()
    {
        // Props are left out: a ray would land on top of the cabinet or the terminal instead of the floor.
        var paths = new WorkshopGridPaths(_grid, 0, WorkshopGridPaths.SolidProps(_workshop, _grid));

        foreach (var cell in Sampled(paths.Floor))
        {
            var world = paths.WorldXz(cell);
            var top = GroundHeightAt(new Vector3(world.X, 0f, world.Y));

            AssertBool(top.HasValue && Mathf.Abs(top.Value - _workshop.GlobalPosition.Y) < GroundTolerance)
                .OverrideFailureMessage($"ground at {world} is {top}")
                .IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OuterWallClosesEverySideOfTheSquare()
    {
        var inner = WorkshopHallLayout.HalfSize - WorkshopHallLayout.WallThickness;
        var origin = _workshop.GlobalPosition;

        for (var along = -inner + 0.5f; along < inner; along += 1f)
        {
            AssertBool(HitsWall(origin + new Vector3(along, 0f, -inner + 0.5f), Vector3.Back * -2f)).IsTrue();
            AssertBool(HitsWall(origin + new Vector3(along, 0f, inner - 0.5f), Vector3.Back * 2f)).IsTrue();
            AssertBool(HitsWall(origin + new Vector3(-inner + 0.5f, 0f, along), Vector3.Left * 2f)).IsTrue();
            AssertBool(HitsWall(origin + new Vector3(inner - 0.5f, 0f, along), Vector3.Right * 2f)).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CeilingKeepsAJumpingPlayerInsideOverTheClearFloor()
    {
        var paths = new WorkshopGridPaths(_grid, ClearanceCells);

        foreach (var cell in Sampled(paths.Clear))
        {
            var world = paths.WorldXz(cell);
            var hit = Ray(new Vector3(world.X, RayStartHeight, world.Y), new Vector3(world.X, RayStartHeight + 8f, world.Y));

            AssertBool(hit.Count > 0 && hit["position"].AsVector3().Y < CeilingMaxHeight).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerCapsuleFitsAcrossTheReachableFloor()
    {
        var paths = new WorkshopGridPaths(_grid, ClearanceCells, WorkshopGridPaths.SolidProps(_workshop, _grid));
        var shape = (CapsuleShape3D)_player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var probe = new ShopSceneProbe(_shop, shape);
        var reachable = paths.ReachableFrom(paths.CellOf(_workshop.GetNode<Node3D>("WorkshopEntry").GlobalPosition));

        foreach (var cell in Sampled(reachable))
        {
            var world = paths.WorldXz(cell);
            var centre = new Vector3(world.X, probe.CapsuleCenterHeight, world.Y);

            AssertBool(probe.CapsuleFits(centre)).OverrideFailureMessage($"capsule does not fit at {centre}").IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task MovementQueriesStayCheapAmongTheSmallCells()
    {
        var hallway = _grid.GetNode<Node3D>("Sections/NorthHallway").GlobalPosition;
        _player.GlobalPosition = hallway + Vector3.Up * 1.2f;
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;

        var cells = _grid.GetUsedCells().Count;
        var withGrid = TypicalQueryMicroseconds();

        GD.Print($"[workshop-grid] movement query {withGrid:F1} us with the grid ({cells} cells)");
        AssertBool(withGrid < MaxQueryMicroseconds).IsTrue();
    }

    private static IEnumerable<Vector2I> Sampled(IEnumerable<Vector2I> cells) =>
        cells.Where(c => c.X % SampleStride == 0 && c.Y % SampleStride == 0);

    // What PlayerController does each physics frame is a test move of the capsule, so that is the cost to bound.
    // The median batch is kept: one interrupted batch cannot fail the test, and one lucky batch cannot hide a slowdown.
    private double TypicalQueryMicroseconds()
    {
        var motion = Vector3.Right * 0.1f;
        var batches = new List<double>();
        for (var batch = 0; batch < QueryBatches; batch++)
        {
            var start = Time.GetTicksUsec();
            for (var i = 0; i < QueriesPerBatch; i++)
                _player.TestMove(_player.GlobalTransform, motion);
            batches.Add((Time.GetTicksUsec() - start) / (double)QueriesPerBatch);
        }

        batches.Sort();
        return batches[batches.Count / 2];
    }

    private bool HitsWall(Vector3 from, Vector3 reach)
    {
        var start = from + Vector3.Up * WallProbeHeight;
        return Ray(start, start + reach).Count > 0;
    }

    private float? GroundHeightAt(Vector3 world)
    {
        var hit = Ray(new Vector3(world.X, RayStartHeight, world.Z), new Vector3(world.X, -RayStartHeight, world.Z));
        return hit.Count > 0 ? hit["position"].AsVector3().Y : null;
    }

    private Godot.Collections.Dictionary Ray(Vector3 from, Vector3 to)
    {
        return _shop.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
    }
}
