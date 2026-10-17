using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.Features.Worldgen.Support;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.MapGeneratorScenarios;

/// <summary>
///     WfcMapGeneratorValidityTest scenarios split out of WfcMapGeneratorIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcMapGeneratorValidityTest : WfcMapGeneratorIntegrationTestBase
{
    [TestCase]
    public void TestGeneratesValidMapWithForestBiome()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("forest");

        AssertThat(biome).IsNotNull();

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        WfcTestFixtures.AssertSucceeded(result, "Forest 10x10 map");
        AssertThat(result.TileIds).IsNotNull();
        AssertThat(result.Size).IsEqual(new Vector2I(10, 10));
    }

    [TestCase]
    public void TestGeneratesValidMapWithPlainsBiome()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");

        AssertThat(biome).IsNotNull();

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        WfcTestFixtures.AssertSucceeded(result, "Plains 10x10 map");
        AssertThat(result.TileIds).IsNotNull();
    }

    /// <summary>
    ///     Seeds and size of the forced-boundary maps that TestNoInvalidAdjacencies checks.
    /// </summary>
    private static readonly ulong[] ForcedBoundarySeeds = { 1, 2, 3, 4, 5 };
    private static readonly Vector2I ForcedBoundarySize = new(16, 16);
    private static readonly string[] ForcedBoundaryAutoTiles = { "A1", "A2", "B" };

    [TestCase]
    public static void TestNoInvalidAdjacencies()
    {
        // Each seed gets its own rules and generator. WfcMapGenerator adds the gap-tile adjacencies to the rules it is
        // given, so every map is checked against the rules its own solver enforced.
        var catalog = new ForcedBoundaryCatalog();
        var biome = ForcedBoundaryBiome(catalog);
        var maps = ForcedBoundarySeeds.Select(seed =>
        {
            var rules = ForcedBoundaryRules();
            var generator = new WfcMapGenerator(rules, catalog) { EnableConnectivity = false };
            return (Seed: seed, Rules: rules, Result: generator.Generate(biome, ForcedBoundarySize, seed));
        }).ToList();

        var failures = maps
            .Where(map => !map.Result.Success)
            .Select(map => $"seed {map.Seed}: generation failed: {map.Result.ErrorMessage}")
            .ToList();
        var adjacency = new List<string>();
        var terrain = new List<string>();
        var edge = new List<string>();
        foreach (var map in maps.Where(map => map.Result.Success))
        {
            // Zero violations prove something only when every auto-tile is on the map, so the solver had to keep
            // A1 from A2 and terrain a from terrain b.
            var counts = CountTiles(map.Result.TileIds!);
            GD.Print($"[ForcedBoundary] seed={map.Seed} tiles: " +
                     string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")));
            var missing = ForcedBoundaryAutoTiles.Where(tileId => !counts.ContainsKey(tileId)).ToList();
            if (missing.Count > 0)
                failures.Add($"seed {map.Seed}: no {string.Join(", ", missing)} on the map, so no boundary is forced");

            var found = FindBoundaryViolations(map.Result.TileIds!, map.Rules, catalog);
            GD.Print($"[ForcedBoundary] seed={map.Seed} adjacency={found.Adjacency.Count} " +
                     $"terrain={found.Terrain.Count} edge={found.Edge.Count}");
            adjacency.AddRange(found.Adjacency.Select(violation => $"seed {map.Seed}: {violation}"));
            terrain.AddRange(found.Terrain.Select(violation => $"seed {map.Seed}: {violation}"));
            edge.AddRange(found.Edge.Select(violation => $"seed {map.Seed}: {violation}"));
        }

        // Every invariant is counted before the assert, so one run names each invariant a mutation breaks.
        if (adjacency.Count > 0)
            failures.Add($"(i) {adjacency.Count} orthogonal pair(s) break the rules, first {adjacency[0]}");
        if (terrain.Count > 0)
            failures.Add($"(ii) {terrain.Count} pair(s) of different terrain within 8 neighbours, first {terrain[0]}");
        if (edge.Count > 0)
            failures.Add($"(iii) {edge.Count} edge pair(s) of adjacent auto-tiles, first {edge[0]}");
        var message = failures.Count == 0
            ? "No forced-boundary violations"
            : string.Join(System.Environment.NewLine, failures);
        AssertThat(failures.Count == 0).OverrideFailureMessage(message).IsTrue();
    }

    private static readonly ulong[] ProductionAdjacencySeeds = { 1, 2, 3 };
    private static readonly Vector2I ProductionAdjacencySize = new(32, 32);

    /// <summary>
    ///     Production tiles and transition pairs: every orthogonal pair on a generated map is one the rules allow.
    /// </summary>
    /// <remarks>
    ///     A regression guard on the real rules, not a mutation test. With the gap-tile adjacencies added, the
    ///     production rules forbid only one pair of the plains candidates (hedge1 beside hedge2), so this case stays
    ///     green when the propagator ignores the rules. TestNoInvalidAdjacencies is the case that fails then. Forest
    ///     has the same candidates and, with uniform base weights, the same maps, so only plains is generated.
    /// </remarks>
    [TestCase]
    public void TestProductionMapsObeyAdjacencyRules()
    {
        var biome = _biomeRegistry.GetBiome("plains");
        AssertThat(biome).IsNotNull();
        var failures = new List<string>();
        foreach (var seed in ProductionAdjacencySeeds)
        {
            // The generator adds the gap-tile adjacencies to these rules, so the map is checked against the rules
            // its own solver enforced. Connectivity is off so that only the adjacency rules decide the map.
            var rules = new WfcAdjacencyRules(_transitionPairs);
            var generator = new WfcMapGenerator(rules, WfcTestFixtures.ProductionCatalog())
            {
                EnableConnectivity = false,
            };
            var result = generator.Generate(biome!, ProductionAdjacencySize, seed);
            WfcTestFixtures.AssertSucceeded(result, $"Plains 32x32 map, seed {seed}");

            var violations = new List<string>();
            CollectRuleViolations(result.TileIds!, rules, violations);
            GD.Print($"[ProductionAdjacency] seed={seed} violations={violations.Count}");
            failures.AddRange(violations.Select(violation => $"seed {seed}: {violation}"));
        }

        var message = failures.Count == 0
            ? "No adjacency violations"
            : $"{failures.Count} orthogonal pair(s) break the rules, first {failures[0]}";
        AssertThat(failures.Count == 0).OverrideFailureMessage(message).IsTrue();
    }

    private static Dictionary<string, int> CountTiles(string[,] tiles)
    {
        var counts = new Dictionary<string, int>();
        foreach (var tileId in tiles)
        {
            counts[tileId] = counts.TryGetValue(tileId, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static WfcAdjacencyRules ForcedBoundaryRules() => new WfcAdjacencyRules(new[]
    {
        ("A1", "A1"), ("A2", "A2"), ("B", "B"), ("A1", "B"), ("A2", "B"),
    });

    private static BiomeDefinition ForcedBoundaryBiome(IWfcTileCatalog catalog)
    {
        var passable = new TilePool.Builder();
        foreach (var tileId in catalog.TileIds)
        {
            passable.Add(tileId);
        }

        return new BiomeDefinition("forced", new CardSignature(), passable.Build(), TilePool.Empty, 0.0f);
    }

    /// <summary>
    ///     The three forced-boundary violations found in one map. Each entry names the first cell and tiles involved.
    /// </summary>
    private sealed record BoundaryViolations(List<string> Adjacency, List<string> Terrain, List<string> Edge);

    /// <summary>
    ///     Finds the three forced-boundary violations in one map.
    /// </summary>
    /// <remarks>
    ///     Finding, not asserted: only map edges are checked for (iii). BitmaskValidityConstraint returns neutral for
    ///     gap-tile candidates (it checks auto-tile placements only), so a gap tile placed beside two auto-tiles
    ///     can complete a straight-edge bitmask inside the map that no check rejects. Edge corners outside the map are
    ///     known when an auto-tile is placed, so the constraint decides edge pairs in full.
    /// </remarks>
    private static BoundaryViolations FindBoundaryViolations(
        string[,] tiles,
        WfcAdjacencyRules rules,
        IWfcTileCatalog catalog)
    {
        var violations = new BoundaryViolations(new List<string>(), new List<string>(), new List<string>());
        CollectRuleViolations(tiles, rules, violations.Adjacency);
        CollectTerrainViolations(tiles, catalog, violations.Terrain);
        CollectEdgeViolations(tiles, catalog, violations.Edge);
        return violations;
    }

    private static void CollectRuleViolations(string[,] tiles, WfcAdjacencyRules rules, List<string> found)
    {
        var height = tiles.GetLength(0);
        var width = tiles.GetLength(1);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (x + 1 < width && !rules.CanBeAdjacent(tiles[y, x], tiles[y, x + 1]))
                    found.Add($"({x},{y}) {tiles[y, x]} beside ({x + 1},{y}) {tiles[y, x + 1]}");
                if (y + 1 < height && !rules.CanBeAdjacent(tiles[y, x], tiles[y + 1, x]))
                    found.Add($"({x},{y}) {tiles[y, x]} above ({x},{y + 1}) {tiles[y + 1, x]}");
            }
        }
    }

    /// <summary>
    ///     East, south, south-east and south-west: each unordered 8-neighbour pair is visited once.
    /// </summary>
    private static readonly (int Dx, int Dy)[] Forward8Neighbours = { (1, 0), (0, 1), (1, 1), (-1, 1) };

    private static void CollectTerrainViolations(string[,] tiles, IWfcTileCatalog catalog, List<string> found)
    {
        var height = tiles.GetLength(0);
        var width = tiles.GetLength(1);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                foreach (var (dx, dy) in Forward8Neighbours)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    var inside = nx >= 0 && nx < width && ny < height;
                    if (inside && catalog.IsAutoTile(tiles[y, x]) && catalog.IsAutoTile(tiles[ny, nx]) &&
                        !catalog.AreSameTerrainType(tiles[y, x], tiles[ny, nx]))
                    {
                        found.Add($"({x},{y}) {tiles[y, x]} beside ({nx},{ny}) {tiles[ny, nx]}");
                    }
                }
            }
        }
    }

    private static void CollectEdgeViolations(string[,] tiles, IWfcTileCatalog catalog, List<string> found)
    {
        var height = tiles.GetLength(0);
        var width = tiles.GetLength(1);

        void AddIfAutoPair(int ax, int ay, int bx, int by)
        {
            if (catalog.IsAutoTile(tiles[ay, ax]) && catalog.IsAutoTile(tiles[by, bx]))
                found.Add($"({ax},{ay}) {tiles[ay, ax]} beside ({bx},{by}) {tiles[by, bx]}");
        }

        for (var x = 0; x + 1 < width; x++)
        {
            AddIfAutoPair(x, 0, x + 1, 0);
            AddIfAutoPair(x, height - 1, x + 1, height - 1);
        }

        for (var y = 0; y + 1 < height; y++)
        {
            AddIfAutoPair(0, y, 0, y + 1);
            AddIfAutoPair(width - 1, y, width - 1, y + 1);
        }
    }

    // Max-of-3 average of three process runs, measured 2026-10-10 on AMD Ryzen 7 7435HS with the production tile
    // catalog and an optimized build (-p:Optimize=true): run averages 760.8, 710.7 and 736.0 ms. See
    // WfcTestFixtures.PerfBudgetMs.
    private const double OptimizedBaseline20x20Ms = 760.8;

    [TestCase]
    public void TestPerformance20x20WithinBudget()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");
        AssertThat(biome).IsNotNull();
        WfcTestFixtures.WarmUp(_transitionPairs, biome!);

        const int runs = 3;
        var times = new double[runs];
        for (var i = 0; i < runs; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = generator.Generate(biome!, new Vector2I(20, 20), 12345);
            stopwatch.Stop();

            WfcTestFixtures.AssertSucceeded(result, "Plains 20x20 map");
            times[i] = stopwatch.Elapsed.TotalMilliseconds;
        }

        var avgTime = times.Average();
        GD.Print($"WFC generation for 20x20 ({runs} runs): average {avgTime:F1}ms, " +
                 $"min {times.Min():F1}ms, max {times.Max():F1}ms");
        WfcTestFixtures.AssertWithinBudget("20x20", avgTime, OptimizedBaseline20x20Ms);
    }

    /// <summary>
    ///     Four tiles that force auto-tile boundaries. A1 and A2 share terrain a but may not touch each other, B is an
    ///     auto-tile of terrain b, and G is the gap tile.
    /// </summary>
    private sealed class ForcedBoundaryCatalog : IWfcTileCatalog
    {
        private static readonly string[] Ids = { "A1", "A2", "B", "G" };

        public IEnumerable<string> TileIds => Ids;

        public bool Contains(string tileId) => tileId is "A1" or "A2" or "B" or "G";

        public bool IsAutoTile(string tileId) => TerrainOf(tileId) != null;

        public bool HasSolidFillVariant(string tileId) => IsAutoTile(tileId);

        public bool IsPassable(string tileId) => Contains(tileId);

        public float GetProbability(string tileId) => 1.0f;

        public bool HasBiomeRestriction(string tileId) => false;

        public bool IsAllowedInBiome(string tileId, string biomeId) => Contains(tileId);

        public bool AreSameTerrainType(string? tileId1, string? tileId2)
        {
            if (tileId1 == null || tileId2 == null)
                return false;

            if (tileId1 == tileId2)
                return true;

            var terrain = TerrainOf(tileId1);
            return terrain != null && terrain == TerrainOf(tileId2);
        }

        public WfcTileVariation? GetVariation(string tileId) => null;

        public bool? IsBitmaskAllowed(string tileId, int bitmask)
        {
            if (!IsAutoTile(tileId))
                return null;

            // Straight edges (two adjacent corners of one side filled, the other two empty) are not drawn.
            return bitmask is not (3 or 6 or 9 or 12);
        }

        public (Vector2I Size, Vector2I Offset)? GetMultiCellBounds(string tileId) => null;

        private static string? TerrainOf(string tileId) => tileId switch
        {
            "A1" or "A2" => "a",
            "B" => "b",
            _ => null,
        };
    }
}
