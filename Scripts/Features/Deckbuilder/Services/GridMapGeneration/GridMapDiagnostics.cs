using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;

internal static class GridMapDiagnostics
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
}
