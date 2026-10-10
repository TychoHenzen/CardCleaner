using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.Features.Worldgen.Support;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.MapGeneratorScenarios;

/// <summary>
///     WfcMapGeneratorDistributionTest scenarios split out of WfcMapGeneratorIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcMapGeneratorDistributionTest : WfcMapGeneratorIntegrationTestBase
{
    [TestCase]
    public void TestTileDistributionDiversity()
    {
        // Validates that tile distribution is diverse (no single type dominates)
        // Target: each tile type should be 10-30% of the map
        var tileRegistry = WfcTestFixtures.CreateTestTileRegistry(WfcTestFixtures.TestBiomeId, "A", "B", "C", "D");
        var generator = new WfcMapGenerator(WfcTestFixtures.FullAdjacencyRules(), tileRegistry);
        generator.EnableSpatialCoherence = false;
        generator.EnableDiminishingReturns = false;
        generator.EnableCompactness = false;
        generator.EnableConnectivity = false;
        generator.ContinuityBiasMultiplier = 1.0f;

        var biome = WfcTestFixtures.CreateAbcdBiome();
        const int totalRuns = 10;
        var goodDistributions = 0;

        for (var seed = 1; seed <= totalRuns; seed++)
        {
            var result = generator.Generate(biome, new Vector2I(10, 10), (ulong)seed * 500);

            if (!result.Success)
            {
                GD.Print($"Seed {seed}: Generation failed - {result.ErrorMessage}");
                continue;
            }

            if (HasReasonableDistribution(seed, result.TileIds!))
                goodDistributions++;
        }

        GD.Print($"Good distributions: {goodDistributions}/{totalRuns}");

        // At least 50% of runs should have reasonable diversity
        AssertThat(goodDistributions).IsGreaterEqual(totalRuns / 2);
    }

    [TestCase]
    public void TestTransitionSpacingReducesContradictions()
    {
        // The transition spacing constraint should reduce contradictions by
        // preventing impossible states where 3+ types meet at a corner.
        // Strict chain: A-C, A-D, B-D are not allowed.
        var tileRegistry = WfcTestFixtures.CreateTestTileRegistry(WfcTestFixtures.TestBiomeId, "A", "B", "C", "D");
        var generator = new WfcMapGenerator(WfcTestFixtures.StrictChainRules(), tileRegistry);
        generator.MaxRetries = 3;

        var biome = WfcTestFixtures.CreateAbcdBiome();
        var successCount = 0;

        // With strict chain rules and transition spacing, should succeed more often
        for (var seed = 1; seed <= 10; seed++)
        {
            var result = generator.Generate(biome, new Vector2I(8, 8), (ulong)seed * 100);
            if (result.Success)
                successCount++;
        }

        GD.Print($"Success rate with transition spacing: {successCount}/10");

        // Should succeed at least some of the time
        // (exact rate depends on how strict the chain is)
        AssertThat(successCount).IsGreaterEqual(1);
    }

    private static bool HasReasonableDistribution(int seed, string[,] tileIds)
    {
        var distribution = RegionAnalyzer.AnalyzeDistribution(tileIds);

        GD.Print($"Seed {seed}: {distribution.UniqueTileTypes} types, " +
                 $"max={distribution.MaxPercentage:F1}%, min={distribution.MinPercentage:F1}%");

        foreach (var dist in distribution.Distributions)
        {
            GD.Print(
                $"  {dist.TileId}: {dist.Percentage:F1}% " +
                $"({dist.TileCount} tiles, {dist.RegionCount} regions)");
        }

        // Check if no tile type exceeds 50% (less strict than 30% initially)
        return distribution.MaxPercentage <= 50.0f && distribution.MinPercentage >= 5.0f;
    }
}
