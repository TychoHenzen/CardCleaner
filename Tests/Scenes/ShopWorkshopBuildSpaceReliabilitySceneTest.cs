using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Workshop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Reliability of the open build space: floors are solid at ground level everywhere including across the
/// doorway threshold, no floor cell opens onto the void, the ceiling keeps a jumping player inside, the
/// player's capsule fits along every clear cell, and the full layout keeps the physics step cheap.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopBuildSpaceReliabilitySceneTest
{
    private const float GroundTolerance = 0.002f;
    private const float RayStartHeight = 1f;
    private const float CeilingMaxHeight = 4.05f;
    private const int ClearanceCells = 4;
    private const int CapsuleSampleStride = 5;
    private const int ThresholdCellX = 30;
    private const int RoomHalfDepthCells = 30;
    private const int QueryBatches = 5;
    private const int QueriesPerBatch = 200;
    private const double MaxQueryMicroseconds = 1000.0;
    private const int SettleFrames = 20;

    private static readonly Vector2I[] Neighbours = [Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up];

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
    public void EveryFloorCellHasSolidGroundAtGroundLevel()
    {
        var groundY = _workshop.GlobalPosition.Y;

        foreach (var cell in _grid.GetUsedCellsByItem(WorkshopGridPaths.FloorItem))
        {
            var top = GroundHeightAt(_grid.CellToWorld(cell));

            AssertBool(top.HasValue && Mathf.Abs(top.Value - groundY) < GroundTolerance).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GroundIsLevelAndContinuousAcrossTheDoorwayThreshold()
    {
        var thresholdX = _workshop.GlobalPosition.X + 6f;

        for (var dx = -0.8f; dx <= 0.8f; dx += 0.05f)
        for (var dz = -1.8f; dz <= 1.8f; dz += 0.3f)
        {
            var top = GroundHeightAt(new Vector3(thresholdX + dx, 0f, _workshop.GlobalPosition.Z + dz));

            AssertBool(top.HasValue && Mathf.Abs(top.Value - _workshop.GlobalPosition.Y) < GroundTolerance).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NoFloorCellOpensOntoTheVoid()
    {
        var built = _grid.GetUsedCells().Select(c => new Vector2I(c.X, c.Z)).ToHashSet();

        foreach (var cell in _grid.GetUsedCellsByItem(WorkshopGridPaths.FloorItem))
        foreach (var step in Neighbours)
        {
            var next = new Vector2I(cell.X, cell.Z) + step;
            AssertBool(built.Contains(next) || IsDoorwayThreshold(next)).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CeilingKeepsAJumpingPlayerInsideOverEveryClearCell()
    {
        var paths = new WorkshopGridPaths(_grid, ClearanceCells);

        foreach (var cell in paths.Clear)
        {
            var world = paths.WorldXz(cell);
            var hit = Ray(new Vector3(world.X, RayStartHeight, world.Y), new Vector3(world.X, RayStartHeight + 8f, world.Y));

            AssertBool(hit.Count > 0 && hit["position"].AsVector3().Y < CeilingMaxHeight).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerCapsuleFitsOnEveryClearCellOfTheReachableLayout()
    {
        var paths = new WorkshopGridPaths(_grid, ClearanceCells);
        var shape = (CapsuleShape3D)_player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var probe = new ShopSceneProbe(_shop, shape);
        var reachable = paths.ReachableFrom(paths.CellOf(_grid.GetNode<Node3D>("Doorway").GlobalPosition));

        foreach (var cell in reachable.Where(c => (c.X + c.Y) % CapsuleSampleStride == 0))
        {
            var world = paths.WorldXz(cell);
            var centre = new Vector3(world.X, probe.CapsuleCenterHeight, world.Y);

            AssertBool(probe.CapsuleFits(centre)).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task MovementQueriesStayCheapAmongTheSmallCells()
    {
        var crossing = _grid.GetNode<Node3D>("Junctions/SpineCrossing").GlobalPosition;
        _player.GlobalPosition = crossing + Vector3.Up * 1.2f;
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;

        var cells = _grid.GetUsedCells().Count;
        var withGrid = FastestQueryMicroseconds();
        _grid.GetParent().RemoveChild(_grid);
        var withoutGrid = FastestQueryMicroseconds();
        _grid.Free();

        GD.Print($"[workshop-grid] movement query {withGrid:F1} us with the grid ({cells} cells), {withoutGrid:F1} us without it");
        AssertBool(withGrid < MaxQueryMicroseconds).IsTrue();
    }

    // What PlayerController does each physics frame is a test move of the capsule, so that is the cost to bound.
    // The fastest batch is kept: a batch only gets slower when something else on the machine interrupts it.
    private double FastestQueryMicroseconds()
    {
        var motion = Vector3.Right * 0.1f;
        var fastest = double.MaxValue;
        for (var batch = 0; batch < QueryBatches; batch++)
        {
            var start = Time.GetTicksUsec();
            for (var i = 0; i < QueriesPerBatch; i++)
                _player.TestMove(_player.GlobalTransform, motion);
            fastest = Mathf.Min((float)fastest, (Time.GetTicksUsec() - start) / (float)QueriesPerBatch);
        }

        return fastest;
    }

    // The cells just west of the grid are the workshop room itself: open at the doorway, its own wall elsewhere.
    private static bool IsDoorwayThreshold(Vector2I cell)
    {
        return cell.X == ThresholdCellX - 1 && Mathf.Abs(cell.Y) < RoomHalfDepthCells;
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
