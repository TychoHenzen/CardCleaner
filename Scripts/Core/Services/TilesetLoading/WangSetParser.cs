using System.Collections.Generic;
using System.Xml.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Parses Wang set data from Tiled TSX tileset files.
/// Converts Tiled's wangid format to bitmasks for auto-tiling.
/// </summary>
internal static class WangSetParser
{
    /// <summary>
    /// Parse all Wang sets and build bitmask → tile mappings.
    /// Multiple tiles per bitmask = variations.
    /// </summary>
    public static WangSetData ParseWangSets(XElement tileset)
    {
        var result = new WangSetData();
        var wangsets = tileset.Element("wangsets");
        if (wangsets == null) return result;

        foreach (var wangset in wangsets.Elements("wangset"))
        {
            var wangType = wangset.Attribute("type")?.Value ?? "corner";
            // Use 'class' attribute as the tile ID, fall back to 'name' for compatibility
            var setId = wangset.Attribute("class")?.Value ?? wangset.Attribute("name")?.Value ?? "unnamed";
            var setName = wangset.Attribute("name")?.Value ?? setId;

            // Parse which color index represents "terrain present"
            // Typically color 1 = foreground/terrain, color 2 = background
            var terrainColorIndex = 1; // Default assumption

            // Check wangset properties for custom terrain color and other properties
            var setProps = TilePropertyParser.ParseProperties(wangset.Element("properties"));
            if (setProps.TryGetValue("terraincolor", out var tcVal))
                terrainColorIndex = ParseHelpers.ParseInt(tcVal, 1);

            // Determine bitmask type from wang type
            var bitmaskType = wangType switch
            {
                "corner" => BitmaskType.Corner4,
                "edge" => BitmaskType.Edge4,
                "mixed" => BitmaskType.Full8,
                _ => BitmaskType.Corner4
            };

            // Store Wang set metadata for later TileDefinition creation
            // setId is the tile ID (from class attr), setName is the display name
            result.WangSetInfo[setId] = new WangSetInfo(setName, bitmaskType, setProps);

            foreach (var wangtile in wangset.Elements("wangtile"))
            {
                var tileId = ParseHelpers.ParseInt(wangtile.Attribute("tileid")?.Value, -1);
                var wangidStr = wangtile.Attribute("wangid")?.Value ?? "";
                var probability = ParseHelpers.ParseFloat(wangtile.Attribute("probability")?.Value, 1f);

                if (tileId < 0 || string.IsNullOrEmpty(wangidStr)) continue;

                var bitmask = WangIdToBitmask(wangidStr, wangType, terrainColorIndex);

                // Track which tiles belong to this wang set (use setId for lookups)
                // This is needed for gap tile detection regardless of blob compliance
                if (!result.TileToWangSet.ContainsKey(tileId))
                    result.TileToWangSet[tileId] = setId;

                // Track bitmask type per tile
                if (!result.TileBitmaskType.ContainsKey(tileId))
                    result.TileBitmaskType[tileId] = bitmaskType;

                // For mixed wangsets (used as Corner16), only add tiles whose edges
                // match what the blob constraint would derive from their corners.
                // This ensures we pick the canonical tile for each corner combination.
                if (wangType == "mixed" && !IsBlobCompliant(wangidStr, terrainColorIndex))
                    continue;

                // Build bitmask → tiles mapping (grouped by wang set ID)
                var key = (setId, bitmask);
                if (!result.BitmaskToTiles.ContainsKey(key))
                    result.BitmaskToTiles[key] = [];

                result.BitmaskToTiles[key].Add(new WangTileInfo(tileId, probability));
            }
        }

        return result;
    }

    /// <summary>
    /// Convert Tiled wangid string to bitmask value.
    /// </summary>
    private static int WangIdToBitmask(string wangidStr, string wangType, int terrainColorIndex)
    {
        // wangid format: "edge0,corner0,edge1,corner1,edge2,corner2,edge3,corner3"
        // Indices: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
        var parts = wangidStr.Split(',');
        if (parts.Length != 8) return 0;

        if (wangType == "corner")
        {
            // Tiled wangid corner format: "0,TR,0,BR,0,BL,0,TL" (indices 1,3,5,7)
            var hasNE = ParseHelpers.ParseInt(parts[1], 0) == terrainColorIndex;  // Top-Right = NE
            var hasSE = ParseHelpers.ParseInt(parts[3], 0) == terrainColorIndex;  // Bottom-Right = SE
            var hasSW = ParseHelpers.ParseInt(parts[5], 0) == terrainColorIndex;  // Bottom-Left = SW
            var hasNW = ParseHelpers.ParseInt(parts[7], 0) == terrainColorIndex;  // Top-Left = NW

            // Convert to Full8 format: derive edges from adjacent corners
            return DualGridAutoTile.CornersToFull8Bitmask(hasNE, hasSE, hasSW, hasNW);
        }

        if (wangType == "edge")
        {
            // Edge mode: indices 0,2,4,6 = Top,Right,Bottom,Left
            var hasN = ParseHelpers.ParseInt(parts[0], 0) == terrainColorIndex;
            var hasE = ParseHelpers.ParseInt(parts[2], 0) == terrainColorIndex;
            var hasS = ParseHelpers.ParseInt(parts[4], 0) == terrainColorIndex;
            var hasW = ParseHelpers.ParseInt(parts[6], 0) == terrainColorIndex;

            // Convert to Full8 format (edges only, no corners)
            int bitmask = 0;
            if (hasN) bitmask |= NeighborBitmask8.North;
            if (hasE) bitmask |= NeighborBitmask8.East;
            if (hasS) bitmask |= NeighborBitmask8.South;
            if (hasW) bitmask |= NeighborBitmask8.West;
            return bitmask;
        }

        if (wangType == "mixed")
        {
            // Mixed format: extract corners and use CornersToFull8Bitmask for consistent lookup keys
            var hasNE = ParseHelpers.ParseInt(parts[1], 0) == terrainColorIndex;
            var hasSE = ParseHelpers.ParseInt(parts[3], 0) == terrainColorIndex;
            var hasSW = ParseHelpers.ParseInt(parts[5], 0) == terrainColorIndex;
            var hasNW = ParseHelpers.ParseInt(parts[7], 0) == terrainColorIndex;

            return DualGridAutoTile.CornersToFull8Bitmask(hasNE, hasSE, hasSW, hasNW);
        }

        return 0;
    }

    /// <summary>
    /// Check if a mixed wangid has blob-compliant edges (edges match what corners imply).
    /// For dual-grid auto-tiling, we only want tiles where edges are derived from corners.
    /// </summary>
    private static bool IsBlobCompliant(string wangidStr, int terrainColorIndex)
    {
        var parts = wangidStr.Split(',');
        if (parts.Length != 8) return false;

        // Parse all values: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
        var hasN = ParseHelpers.ParseInt(parts[0], 0) == terrainColorIndex;
        var hasNE = ParseHelpers.ParseInt(parts[1], 0) == terrainColorIndex;
        var hasE = ParseHelpers.ParseInt(parts[2], 0) == terrainColorIndex;
        var hasSE = ParseHelpers.ParseInt(parts[3], 0) == terrainColorIndex;
        var hasS = ParseHelpers.ParseInt(parts[4], 0) == terrainColorIndex;
        var hasSW = ParseHelpers.ParseInt(parts[5], 0) == terrainColorIndex;
        var hasW = ParseHelpers.ParseInt(parts[6], 0) == terrainColorIndex;
        var hasNW = ParseHelpers.ParseInt(parts[7], 0) == terrainColorIndex;

        // Compute what blob-normalized edges SHOULD be given the corners:
        // An edge is set if EITHER adjacent corner is set
        var blobN = hasNW || hasNE;
        var blobE = hasNE || hasSE;
        var blobS = hasSE || hasSW;
        var blobW = hasSW || hasNW;

        // Check if actual edges match blob-derived edges
        return hasN == blobN && hasE == blobE && hasS == blobS && hasW == blobW;
    }
}

/// <summary>
/// Data container for parsed Wang sets.
/// </summary>
internal class WangSetData
{
    /// <summary>
    /// Maps (wangSetName, bitmask) → list of tiles with that pattern.
    /// Multiple tiles = variations.
    /// </summary>
    public Dictionary<(string setName, int bitmask), List<WangTileInfo>> BitmaskToTiles { get; } = new();

    /// <summary>
    /// Maps tileId → which wang set it belongs to.
    /// </summary>
    public Dictionary<int, string> TileToWangSet { get; } = new();

    /// <summary>
    /// Maps tileId → bitmask type for that wang set.
    /// </summary>
    public Dictionary<int, BitmaskType> TileBitmaskType { get; } = new();

    /// <summary>
    /// Maps wang set name → info about the set (bitmask type, properties).
    /// </summary>
    public Dictionary<string, WangSetInfo> WangSetInfo { get; } = new();
}

/// <summary>
/// Metadata about a Wang set.
/// </summary>
internal record WangSetInfo(string Name, BitmaskType BitmaskType, Dictionary<string, string> Properties);

/// <summary>
/// Information about a tile within a Wang set.
/// </summary>
internal record WangTileInfo(int TileId, float Probability);
