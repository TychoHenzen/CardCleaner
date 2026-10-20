using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Support;

/// <summary>
///     Shared builders for WFC tests that run on a synthetic four-tile (A, B, C, D) biome.
/// </summary>
public static class WfcTestFixtures
{
    public const string TestBiomeId = "test";

    /// <summary>
    ///     Every pair of A-D tiles may be adjacent.
    /// </summary>
    public static WfcAdjacencyRules FullAdjacencyRules()
    {
        return new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C"),
            ("C", "D"),
            ("A", "D"),
            ("B", "D")
        });
    }

    /// <summary>
    ///     Strict chain: only A-B, B-C and C-D may be adjacent.
    /// </summary>
    public static WfcAdjacencyRules StrictChainRules()
    {
        return new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("C", "D")
        });
    }

    /// <summary>
    ///     A biome whose passable pool contains A, B, C and D with equal weight.
    /// </summary>
    public static BiomeDefinition CreateAbcdBiome()
    {
        var passable = new TilePool.Builder();
        passable.Add("A", 1.0f);
        passable.Add("B", 1.0f);
        passable.Add("C", 1.0f);
        passable.Add("D", 1.0f);

        return new BiomeDefinition(
            TestBiomeId,
            new CardSignature(),
            passable.Build(),
            TilePool.Empty,
            0.0f);
    }

    /// <summary>
    ///     Creates a TileRegistry with the specified tiles allowed in the given biome.
    /// </summary>
    public static TileRegistry CreateTestTileRegistry(string biomeId, params string[] tileIds) =>
        CreateTestTileRegistry(biomeId, tileIds.Select(tileId => (tileId, TilePassability.Passable)).ToArray());

    /// <summary>
    ///     Creates a TileRegistry with the specified tiles, each with its own passability, allowed in the given biome.
    /// </summary>
    public static TileRegistry CreateTestTileRegistry(
        string biomeId,
        params (string Id, TilePassability Passability)[] tiles)
    {
        var registry = new TileRegistry(new GodotAssetPaths(), new CompiledAtlasSource());
        registry.Clear(); // Clear production tiles loaded by constructor
        foreach (var (tileId, passability) in tiles)
        {
            registry.RegisterTile(new TileDefinition(
                tileId,
                tileId,
                passability,
                Vector2I.Zero,
                new TileDefinitionOptions
                {
                    AllowedBiomes = new HashSet<string> { biomeId }
                }));
        }

        return registry;
    }

    /// <summary>
    ///     A catalog over the production TileRegistry, the pairing WfcGridFingerprintTest pins with the
    ///     production transition pairs.
    /// </summary>
    public static TileCatalogData ProductionCatalog() =>
        TileCatalogData.From(new TileRegistry(new GodotAssetPaths(), new CompiledAtlasSource()));

    /// <summary>
    ///     Asserts that a generation succeeded, quoting the generator's error message when it did not.
    /// </summary>
    public static void AssertSucceeded(WfcGenerationResult result, string label = "Generation")
    {
        AssertThat(result.Success)
            .OverrideFailureMessage($"{label} failed: {result.ErrorMessage}")
            .IsTrue();
    }

    /// <summary>
    ///     Multiplier on a measured optimized baseline. Raised only on CI evidence, which goes in the commit that
    ///     raises it.
    /// </summary>
    public const double Headroom = 3.0;

    /// <summary>
    ///     Debug-build cost over the optimized build, rounded up to the next 0.5. The gdUnit runner loads the
    ///     Debug build, so the budget carries this factor. Release builds use 1.
    ///     Measured 2026-10-10 on AMD Ryzen 7 7435HS with the production tile catalog, max-of-3 averages in ms of
    ///     the perf cases, Debug build over an optimized build (-p:Optimize=true): 30x30 3205.3 over 1314.1 (2.44,
    ///     the largest, rounded up to 2.5); 20x20 1088.1 over 760.8 (1.43); 60x60 13813.6 over 10053.6 (1.37, the
    ///     optimized max is inflated by a noisy run).
    /// </summary>
#if DEBUG
    public const double BuildFactor = 2.5;
#else
    public const double BuildFactor = 1.0;
#endif

    /// <summary>
    ///     Time budget for one perf case: the optimized baseline times <see cref="Headroom"/> times
    ///     <see cref="BuildFactor"/>.
    /// </summary>
    public static double PerfBudgetMs(double optimizedBaselineMs) => optimizedBaselineMs * Headroom * BuildFactor;

    /// <summary>
    ///     Prints the measured and budgeted time on every run so CI logs carry them, then asserts it fits.
    /// </summary>
    public static void AssertWithinBudget(string caseName, double measuredMs, double optimizedBaselineMs)
    {
        var budgetMs = PerfBudgetMs(optimizedBaselineMs);
        var ratio = measuredMs / budgetMs;
        GD.Print(FormattableString.Invariant(
            $"[PerfBudget] {caseName} measured={measuredMs:F1}ms budget={budgetMs:F1}ms ratio={ratio:F2}"));
        AssertThat(measuredMs)
            .OverrideFailureMessage(FormattableString.Invariant(
                $"{caseName} averaged {measuredMs:F1}ms, over its budget of {budgetMs:F1}ms"))
            .IsLess(budgetMs);
    }

    /// <summary>
    ///     One untimed 10x10 generation on a fresh generator, so JIT cost stays out of the timed runs.
    /// </summary>
    public static void WarmUp(IReadOnlyList<(string tileA, string tileB)> transitionPairs, BiomeDefinition biome)
    {
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(transitionPairs), ProductionCatalog());
        AssertSucceeded(generator.Generate(biome, new Vector2I(10, 10), 12345), "10x10 warm-up");
    }
}
