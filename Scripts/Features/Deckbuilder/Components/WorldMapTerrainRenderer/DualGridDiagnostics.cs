using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

/// <summary>
/// Logs the summary and the bitmask checks of a dual-grid overlay set built by <see cref="DualGridOverlayBuilder"/>.
/// </summary>
internal static class DualGridDiagnostics
{
    public static void LogSummary(Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> overlays)
    {
        var transitionCount = overlays.Count(kvp => kvp.Value.Bitmask > 0 && kvp.Value.Bitmask < 15);
        ILog.Print(
            $"Dual-grid terrain: {overlays.Count} visual tiles, " +
            $"{transitionCount} transitions");
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
