using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;

internal static class GridMapDiagnostics
{
    private const float CoherenceTargetPercent = 70.0f;

    /// <summary>Logs one line on how much of a generated map lies in large same-tile regions.</summary>
    public static void ValidateSpatialCoherence(string[,] tileMap, Vector2I size)
    {
        var metrics = RegionAnalyzer.Analyze(tileMap);
        var verdict = metrics.PercentInLargeRegions < CoherenceTargetPercent ? "below" : "meets";

        ILog.Print(
            $"[SpatialCoherence] {metrics.RegionCount} regions (avg {metrics.AverageSize:F1}, " +
            $"min {metrics.MinSize}, max {metrics.MaxSize} tiles); {metrics.PercentInLargeRegions:F1}% of tiles " +
            $"({metrics.TilesInLargeRegions}/{metrics.TotalTiles}) in regions >= 30 tiles, {verdict} the " +
            $"{CoherenceTargetPercent:F0}% target");
    }
}
