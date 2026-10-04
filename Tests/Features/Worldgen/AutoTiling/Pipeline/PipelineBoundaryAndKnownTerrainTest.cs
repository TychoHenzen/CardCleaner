using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.Pipeline;

/// <summary>
///     PipelineBoundaryAndKnownTerrainTest scenarios split out of PipelineIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PipelineBoundaryAndKnownTerrainTest : PipelineIntegrationTestBase
{
    /// <summary>
    /// Test with a horizontal boundary (top half terrain1, bottom half terrain2).
    /// </summary>
    [TestCase]
    public void TestHorizontalBoundary()
    {
        const string terrain1 = "grass";
        const string terrain2 = "sand";
        const int dataWidth = 5;
        const int dataHeight = 4;

        // Top 2 rows are terrain1, bottom 2 rows are terrain2
        bool IsTerrain1At(int x, int y)
        {
            return x >= 0 && x < dataWidth && y >= 0 && y < 2;
        }

        // Visual tile row vy=2 is on the boundary: it samples data rows 1 (terrain1) and 2 (terrain2),
        // so NW and NE are set and SW and SE are unset.
        var boundaryTiles = EnumerateVisualGrid(dataWidth, dataHeight)
            .Where(cell => cell.Y == 2 && cell.X > 0 && cell.X < dataWidth)
            .Select(cell => new ResolvedCell(cell, DualGridAutoTile.ComputeBitmask(cell.X, cell.Y, IsTerrain1At), null))
            .ToList();

        // Expected: NW(8) + NE(1) = 9
        var errors = boundaryTiles
            .Where(tile => tile.Bitmask != 9)
            .Select(tile => $"Boundary tile ({tile.Cell.X},{tile.Cell.Y}): " +
                            $"expected bitmask 9 (NW+NE), got {tile.Bitmask}")
            .ToList();

        GD.Print("Horizontal Boundary Tiles:");
        foreach (var tile in boundaryTiles)
        {
            var coords = _resolver.ResolveTransition(terrain1, terrain2, tile.Bitmask);
            GD.Print($"  ({tile.Cell.X},{tile.Cell.Y}): bitmask={tile.Bitmask}, coords={FormatCoords(coords)}");
        }

        PrintErrors("Errors", errors);
        AssertThat(errors.Count).IsEqual(0);
    }

    // ==================== Full Pipeline Validation ====================

    [TestCase]
    public void TestFullPipelineWithKnownTerrain()
    {
        // Use actual terrain IDs from registry
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .Take(2)
            .ToList();

        if (compositableTiles.Count < 2)
        {
            GD.PrintErr("Need at least 2 compositable tiles for pipeline test");
            return;
        }

        var terrain1 = compositableTiles[0].Id;
        var terrain2 = compositableTiles[1].Id;

        GD.Print($"Testing pipeline with terrains: {terrain1}, {terrain2}");

        // 3x3 grid with terrain1 in the center only; outside is terrain2
        const int dataWidth = 3;
        const int dataHeight = 3;

        static bool IsTerrain1At(int x, int y)
        {
            return x == 1 && y == 1;
        }

        var results = EnumerateVisualGrid(dataWidth, dataHeight)
            .Select(cell => ResolveWithFallback(terrain1, terrain2, cell, IsTerrain1At))
            .ToList();
        var validCoords = results.Count(r => r.Coords.HasValue);
        var nullCoords = results.Count - validCoords;

        GD.Print("Full Pipeline Results:");
        foreach (var result in results)
        {
            var status = result.Coords.HasValue ? "OK" : "NULL";
            GD.Print($"  ({result.Cell.X},{result.Cell.Y}): bitmask={result.Bitmask}, status={status}");
        }

        GD.Print(
            $"\nSummary: {validCoords} valid, {nullCoords} null " +
            $"({100.0 * nullCoords / (validCoords + nullCoords):F1}% null)");

        // Should have more valid than null
        AssertThat(validCoords).IsGreater(nullCoords);
    }

    private ResolvedCell ResolveWithFallback(
        string innerTerrain,
        string outerTerrain,
        VisualCell cell,
        System.Func<int, int, bool> isInnerAt)
    {
        // Stage 1: bitmask; Stage 2: terrain pair (simplified, no dominance lookup)
        var bitmask = DualGridAutoTile.ComputeBitmask(cell.X, cell.Y, isInnerAt);

        // Stage 3: resolve transition; Stage 4: fall back to any variant
        var coords = _resolver.ResolveTransition(innerTerrain, outerTerrain, bitmask)
                     ?? _resolver.ResolveAnyVariant(innerTerrain, bitmask);
        return new ResolvedCell(cell, bitmask, coords);
    }
}
