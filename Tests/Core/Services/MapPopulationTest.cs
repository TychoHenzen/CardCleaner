using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// The shared placement rule on a grid map and on a mesh map: a passable start and 2-3 enemies, each at least the
/// minimum distance from the start.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MapPopulationTest
{
    // 40 tiles of 16 world units: from any start, some cell is more than 200 world units away.
    private const int GridSide = 40;

    // The ring count both scenes use; the mesh is wide enough that some cell is far from any start.
    private const int MeshRings = 20;

    // The irregular map's world scale (MeshMapGenerator.WorldScale).
    private const float MeshWorldScale = 16f;

    private const int SeedCount = 20;

    [TestCase]
    [TestCategory("Unit")]
    public static void GridMapGetsAPassableStartAndTwoToThreeFarEnemies()
    {
        var map = GridMap(GridSide);
        for (ulong seed = 1; seed <= SeedCount; seed++)
            AssertPlacementFollowsTheRule(map, seed);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MeshMapGetsAPassableStartAndTwoToThreeFarEnemies()
    {
        var map = MeshMap();
        AssertThat(map.WorldBounds.Size.X).IsGreater(2.5f * MapPopulation.MinEnemyDistance);
        for (ulong seed = 1; seed <= SeedCount; seed++)
            AssertPlacementFollowsTheRule(map, seed);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheSameSeedGivesTheSamePlacement()
    {
        var map = GridMap(GridSide);

        var first = MapPopulation.Place(map, Rng(7));
        var second = MapPopulation.Place(map, Rng(7));

        AssertThat(second.PlayerStartCell).IsEqual(first.PlayerStartCell);
        AssertThat(string.Join(",", second.EnemyCells)).IsEqual(string.Join(",", first.EnemyCells));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheStartIsNotAlwaysTheSameCell()
    {
        var map = GridMap(GridSide);

        var starts = Enumerable.Range(1, SeedCount)
            .Select(seed => MapPopulation.Place(map, Rng((ulong)seed)).PlayerStartCell)
            .Distinct()
            .Count();

        AssertThat(starts).IsGreater(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASmallMapStillGetsEnemiesOnOtherPassableCells()
    {
        // 5 x 16 = 80 world units: no cell is 200 world units from the start.
        var map = GridMap(5);

        var placement = MapPopulation.Place(map, Rng(3));

        AssertThat(placement.EnemyCells.Count).IsBetween(MapPopulation.MinEnemies, MapPopulation.MaxEnemies);
        AssertBool(placement.EnemyCells.Contains(placement.PlayerStartCell)).IsFalse();
        foreach (var enemy in placement.EnemyCells)
            AssertBool(map.IsPassable(enemy)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AStartWithOneFarCellPutsAnEnemyThereAndTopsUpFromNearerCells()
    {
        // A 20 x 1 strip with passable cells 0, 1, 2 and 19: from a start among 0-2 only cell 19 is far (272 world
        // units or more), so two enemies must come from the nearer cells; from 19 all three others are far.
        var cells = new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(2, 0), new Vector2I(19, 0) };
        for (ulong seed = 1; seed <= SeedCount; seed++)
        {
            var map = StripMap(20, cells);
            var placement = MapPopulation.Place(map, Rng(seed));
            var start = map.GetCellCenter(placement.PlayerStartCell);

            AssertThat(placement.EnemyCells.Count).IsBetween(MapPopulation.MinEnemies, MapPopulation.MaxEnemies);
            AssertBool(placement.EnemyCells.Contains(placement.PlayerStartCell)).IsFalse();
            AssertBool(placement.EnemyCells.Any(
                enemy => map.GetCellCenter(enemy).DistanceTo(start) >= MapPopulation.MinEnemyDistance)).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AMapWithOnePassableCellStartsThereWithNoEnemies()
    {
        var only = new Vector2I(2, 3);
        var map = GridMap(5, new[] { only });

        var placement = MapPopulation.Place(map, Rng(3));

        AssertThat(placement.PlayerStartCell).IsEqual(map.PositionToCellId(only));
        AssertThat(placement.EnemyCells.Count).IsEqual(0);
    }

    private static void AssertPlacementFollowsTheRule(IMapData map, ulong seed)
    {
        var placement = MapPopulation.Place(map, Rng(seed));

        AssertBool(map.IsPassable(placement.PlayerStartCell)).IsTrue();
        AssertThat(placement.EnemyCells.Count).IsBetween(MapPopulation.MinEnemies, MapPopulation.MaxEnemies);
        AssertThat(placement.EnemyCells.Distinct().Count()).IsEqual(placement.EnemyCells.Count);
        var start = map.GetCellCenter(placement.PlayerStartCell);
        foreach (var enemy in placement.EnemyCells)
        {
            AssertBool(map.IsPassable(enemy)).IsTrue();
            AssertThat(map.GetCellCenter(enemy).DistanceTo(start)).IsGreaterEqual(MapPopulation.MinEnemyDistance);
        }
    }

    private static RandomNumberGenerator Rng(ulong seed) => new() { Seed = seed };

    private static RegularGridMapData GridMap(int side, Vector2I[]? passable = null)
    {
        var cells = passable?.ToList() ?? (
            from y in Enumerable.Range(0, side)
            from x in Enumerable.Range(0, side)
            select new Vector2I(x, y)).ToList();
        return new RegularGridMapData(new SimpleMapData
        {
            TileIds = new string[side, side],
            Size = new Vector2I(side, side),
            PassableTiles = cells
        });
    }

    private static RegularGridMapData StripMap(int length, Vector2I[] passable) =>
        new(new SimpleMapData
        {
            TileIds = new string[1, length],
            Size = new Vector2I(length, 1),
            PassableTiles = passable.ToList()
        });

    private static IrregularMeshNs.IrregularMeshMapData MeshMap()
    {
        var mesh = IrregularMeshNs.MeshGenerator.Generate(new IrregularMeshNs.MeshGenerator.GenerationConfig
        {
            Rings = MeshRings,
            Seed = 11,
            RelaxationIterations = 0
        });
        foreach (var vertex in mesh.Vertices)
            vertex.TerrainType = 1;
        mesh.UpdateAllCachedProperties();
        return new IrregularMeshNs.IrregularMeshMapData(mesh) { WorldScale = MeshWorldScale };
    }
}
