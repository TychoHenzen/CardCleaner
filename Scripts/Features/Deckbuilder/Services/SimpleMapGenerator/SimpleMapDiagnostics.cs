using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.SimpleMapGeneratorSupport;

internal static class SimpleMapDiagnostics
{
    public static void ValidateSpatialCoherence(string[,] tileMap, Vector2I size)
    {
        var metrics = RegionAnalyzer.Analyze(tileMap);

        ILog.Print(
            $"[SpatialCoherence] {metrics.RegionCount} regions found " +
            $"(avg size: {metrics.AverageSize:F1} tiles)");
        ILog.Print($"[SpatialCoherence] Region sizes: min={metrics.MinSize}, max={metrics.MaxSize}");
        ILog.Print(
            $"[SpatialCoherence] {metrics.PercentInLargeRegions:F1}% of tiles in regions >= 30 tiles " +
            $"({metrics.TilesInLargeRegions}/{metrics.TotalTiles})");

        if (metrics.PercentInLargeRegions < 70.0f)
        {
            ILog.Print(
                $"[SpatialCoherence] WARNING: Low coherence - only {metrics.PercentInLargeRegions:F1}% " +
                "of tiles in large regions (target: 70%+)");
        }
    }

    public static void ValidateBitmaskConsistency(
        Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> overlays,
        int visualWidth,
        int visualHeight)
    {
        var (totalTiles, uniqueTerrains, terrainConflicts, bitmaskViolations) =
            BitmaskConsistencyValidator.Analyze(overlays, visualWidth, visualHeight);

        ILog.Print($"[BitmaskConsistency] {totalTiles} tiles, {uniqueTerrains} unique topTerrains");

        if (terrainConflicts > 0)
        {
            ILog.Print(
                $"[BitmaskConsistency] {terrainConflicts} adjacent tiles with different " +
                "topTerrains (3-way boundaries)");
        }

        if (bitmaskViolations > 0)
        {
            ILog.Print($"[BitmaskConsistency] WARNING: {bitmaskViolations} bitmask violations detected!");
            var violations = BitmaskConsistencyValidator.ValidateConsistency(
                overlays,
                visualWidth,
                visualHeight);
            foreach (var violation in violations.Take(3))
                ILog.Print($"[BitmaskConsistency]   {violation}");

            if (violations.Count > 3)
                ILog.Print($"[BitmaskConsistency]   ... and {violations.Count - 3} more");
        }
        else
        {
            ILog.Print(
                "[BitmaskConsistency] All bitmasks consistent " +
                "(same-terrain adjacencies match)");
        }
    }
}
