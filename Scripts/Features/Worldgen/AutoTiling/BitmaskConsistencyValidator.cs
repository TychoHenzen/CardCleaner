using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
/// Validates bitmask consistency in dual-grid auto-tiling.
/// Adjacent visual tiles share data cells at their edges - their bitmasks must agree
/// on whether those shared cells contain the "top terrain".
/// </summary>
public static class BitmaskConsistencyValidator
{
    /// <summary>
    /// A detected bitmask violation where adjacent tiles disagree on a shared data cell.
    /// </summary>
    public readonly struct Violation
    {
        public Vector2I TileA { get; init; }
        public Vector2I TileB { get; init; }
        public string TileATopTerrain { get; init; }
        public string TileBTopTerrain { get; init; }
        public int TileABitmask { get; init; }
        public int TileBBitmask { get; init; }
        public string CornerNameA { get; init; }
        public string CornerNameB { get; init; }
        public bool TileASaysSet { get; init; }
        public bool TileBSaysSet { get; init; }

        public override string ToString()
        {
            return $"Conflict at {TileA}-{TileB}: " +
                   $"A({TileATopTerrain}, mask={TileABitmask}).{CornerNameA}={TileASaysSet} vs " +
                   $"B({TileBTopTerrain}, mask={TileBBitmask}).{CornerNameB}={TileBSaysSet}";
        }
    }

    /// <summary>
    /// Validates that adjacent visual tiles have consistent bitmasks for shared corners.
    /// </summary>
    /// <param name="overlays">Dictionary of visual positions to (baseTileId, topTileId, bitmask)</param>
    /// <param name="visualWidth">Width of visual grid</param>
    /// <param name="visualHeight">Height of visual grid</param>
    /// <returns>List of violations found</returns>
    public static List<Violation> ValidateConsistency(
        Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> overlays,
        int visualWidth,
        int visualHeight)
    {
        var violations = new List<Violation>();

        for (var vy = 0; vy < visualHeight; vy++)
        for (var vx = 0; vx < visualWidth; vx++)
        {
            var posA = new Vector2I(vx, vy);
            if (!overlays.TryGetValue(posA, out var overlayA))
                continue;

            // Check horizontal neighbor (tile to the right)
            var posB = new Vector2I(vx + 1, vy);
            if (overlays.TryGetValue(posB, out var overlayB))
            {
                // A.NE and B.NW share data cell [vy-1, vx]
                // A.SE and B.SW share data cell [vy, vx]
                CheckSharedCorner(violations, posA, posB, overlayA, overlayB,
                    NeighborBitmaskCorner.NorthEast, NeighborBitmaskCorner.NorthWest, "NE", "NW");
                CheckSharedCorner(violations, posA, posB, overlayA, overlayB,
                    NeighborBitmaskCorner.SouthEast, NeighborBitmaskCorner.SouthWest, "SE", "SW");
            }

            // Check vertical neighbor (tile below)
            posB = new Vector2I(vx, vy + 1);
            if (overlays.TryGetValue(posB, out overlayB))
            {
                // A.SW and B.NW share data cell [vy, vx-1]
                // A.SE and B.NE share data cell [vy, vx]
                CheckSharedCorner(violations, posA, posB, overlayA, overlayB,
                    NeighborBitmaskCorner.SouthWest, NeighborBitmaskCorner.NorthWest, "SW", "NW");
                CheckSharedCorner(violations, posA, posB, overlayA, overlayB,
                    NeighborBitmaskCorner.SouthEast, NeighborBitmaskCorner.NorthEast, "SE", "NE");
            }
        }

        return violations;
    }

    private static void CheckSharedCorner(
        List<Violation> violations,
        Vector2I posA, Vector2I posB,
        (string BaseTileId, string TopTileId, int Bitmask) overlayA,
        (string BaseTileId, string TopTileId, int Bitmask) overlayB,
        int cornerBitA, int cornerBitB,
        string cornerNameA, string cornerNameB)
    {
        // If both tiles have the SAME topTerrain, their bits for shared corners MUST match
        if (overlayA.TopTileId == overlayB.TopTileId)
        {
            var aBitSet = (overlayA.Bitmask & cornerBitA) != 0;
            var bBitSet = (overlayB.Bitmask & cornerBitB) != 0;

            if (aBitSet != bBitSet)
            {
                violations.Add(new Violation
                {
                    TileA = posA,
                    TileB = posB,
                    TileATopTerrain = overlayA.TopTileId,
                    TileBTopTerrain = overlayB.TopTileId,
                    TileABitmask = overlayA.Bitmask,
                    TileBBitmask = overlayB.Bitmask,
                    CornerNameA = cornerNameA,
                    CornerNameB = cornerNameB,
                    TileASaysSet = aBitSet,
                    TileBSaysSet = bBitSet
                });
            }
        }
        // If tiles have DIFFERENT topTerrains, that's a "different terrain" situation
        // which we track separately as it indicates potential 3-way boundary issues
    }

    /// <summary>
    /// Detects visual tiles where adjacent tiles chose different topTerrains.
    /// This indicates potential 3-way boundary issues even if individual bitmasks are internally consistent.
    /// </summary>
    public static List<(Vector2I PosA, Vector2I PosB, string TerrainA, string TerrainB)> DetectTopTerrainConflicts(
        Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> overlays,
        int visualWidth,
        int visualHeight)
    {
        var conflicts = new List<(Vector2I, Vector2I, string, string)>();

        for (var vy = 0; vy < visualHeight; vy++)
        for (var vx = 0; vx < visualWidth; vx++)
        {
            var posA = new Vector2I(vx, vy);
            if (!overlays.TryGetValue(posA, out var overlayA))
                continue;

            // Check right neighbor
            var posB = new Vector2I(vx + 1, vy);
            if (overlays.TryGetValue(posB, out var overlayB))
            {
                if (overlayA.TopTileId != overlayB.TopTileId)
                {
                    conflicts.Add((posA, posB, overlayA.TopTileId, overlayB.TopTileId));
                }
            }

            // Check bottom neighbor
            posB = new Vector2I(vx, vy + 1);
            if (overlays.TryGetValue(posB, out overlayB))
            {
                if (overlayA.TopTileId != overlayB.TopTileId)
                {
                    conflicts.Add((posA, posB, overlayA.TopTileId, overlayB.TopTileId));
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Analyzes a bitmask grid and reports statistics.
    /// </summary>
    public static (int TotalTiles, int UniqueTopTerrains, int TopTerrainConflicts, int BitmaskViolations) Analyze(
        Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> overlays,
        int visualWidth,
        int visualHeight)
    {
        var topTerrains = new HashSet<string>();
        foreach (var overlay in overlays.Values)
        {
            topTerrains.Add(overlay.TopTileId);
        }

        var terrainConflicts = DetectTopTerrainConflicts(overlays, visualWidth, visualHeight);
        var bitmaskViolations = ValidateConsistency(overlays, visualWidth, visualHeight);

        return (overlays.Count, topTerrains.Count, terrainConflicts.Count, bitmaskViolations.Count);
    }
}
