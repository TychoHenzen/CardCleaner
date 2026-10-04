using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.Pipeline;

/// <summary>
///     PipelineUniformAndCheckerTest scenarios split out of PipelineIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PipelineUniformAndCheckerTest : PipelineIntegrationTestBase
{
    // ==================== Fixed Input Grid Tests ====================

    /// <summary>
    /// Test with a simple 3x3 uniform terrain grid.
    /// All visual tiles should resolve to solid fill (bitmask 15).
    /// </summary>
    [TestCase]
    public void TestUniformTerrainGrid()
    {
        // 3x3 data grid of uniform terrain
        const string terrainId = "grass";
        const int dataWidth = 3;
        const int dataHeight = 3;

        bool IsTerrainAt(int x, int y)
        {
            return x >= 0 && x < dataWidth && y >= 0 && y < dataHeight;
        }

        var results = EnumerateVisualGrid(dataWidth, dataHeight)
            .Select(cell => ResolveCell(terrainId, cell, IsTerrainAt))
            .ToList();

        // Visual tiles completely inside data grid should have bitmask 15
        var errors = results
            .Where(r => IsInteriorCell(r.Cell, dataWidth, dataHeight) && r.Bitmask != 15)
            .Select(r => $"Interior tile ({r.Cell.X},{r.Cell.Y}): expected bitmask 15, got {r.Bitmask}")
            .ToList();

        GD.Print("Uniform Terrain Grid Results:");
        foreach (var result in results)
        {
            GD.Print(
                $"  Visual ({result.Cell.X},{result.Cell.Y}): bitmask={result.Bitmask}, " +
                $"coords={FormatCoords(result.Coords)}");
        }

        PrintErrors("Errors", errors);
        AssertThat(errors.Count).IsEqual(0);
    }

    private ResolvedCell ResolveCell(string terrainId, VisualCell cell, System.Func<int, int, bool> isTerrainAt)
    {
        var bitmask = DualGridAutoTile.ComputeBitmask(cell.X, cell.Y, isTerrainAt);

        // Try to resolve the transition, falling back to any variant
        var coords = _resolver.ResolveTransition(terrainId, terrainId, bitmask)
                     ?? _resolver.ResolveAnyVariant(terrainId, bitmask);
        return new ResolvedCell(cell, bitmask, coords);
    }

    /// <summary>
    /// Test with a checkerboard pattern to exercise all boundary conditions.
    /// </summary>
    [TestCase]
    public void TestCheckerboardPattern()
    {
        const string terrain1 = "grass";
        const string terrain2 = "sand";
        var dataWidth = 4;
        var dataHeight = 4;

        // Checkerboard: (x+y) % 2 == 0 is terrain1, else terrain2
        string GetTerrainAt(int x, int y)
        {
            if (x < 0 || x >= dataWidth || y < 0 || y >= dataHeight)
                return ""; // Out of bounds
            return (x + y) % 2 == 0 ? terrain1 : terrain2;
        }

        bool IsTerrain1At(int x, int y) => GetTerrainAt(x, y) == terrain1;

        var visualWidth = dataWidth + 1;
        var visualHeight = dataHeight + 1;

        var bitmaskCounts = new Dictionary<int, int>();

        for (var vy = 0; vy < visualHeight; vy++)
        {
            for (var vx = 0; vx < visualWidth; vx++)
            {
                var bitmask = DualGridAutoTile.ComputeBitmask(vx, vy, IsTerrain1At);

                if (!bitmaskCounts.ContainsKey(bitmask))
                    bitmaskCounts[bitmask] = 0;
                bitmaskCounts[bitmask]++;
            }
        }

        GD.Print("Checkerboard Pattern Bitmask Distribution:");
        foreach (var (bitmask, count) in bitmaskCounts.OrderBy(kvp => kvp.Key))
        {
            GD.Print($"  Bitmask {bitmask}: {count} tiles");
        }

        // Checkerboard should produce specific diagonal patterns
        // No tile should have bitmask 15 (solid) or 0 (empty) in interior
        // Common: 5 (NE+SW), 10 (NW+SE)
        AssertThat(bitmaskCounts.Count).IsGreater(2); // Multiple distinct bitmasks
    }
}
