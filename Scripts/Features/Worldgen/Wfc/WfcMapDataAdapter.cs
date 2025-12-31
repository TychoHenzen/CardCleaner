using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Converts a collapsed WfcGrid into SimpleMapData format for rendering.
/// </summary>
public class WfcMapDataAdapter
{
    /// <summary>
    /// Converts a fully collapsed WFC grid to SimpleMapData.
    /// </summary>
    /// <param name="grid">Fully collapsed WFC grid</param>
    /// <param name="biome">Biome to apply to all cells</param>
    /// <param name="passableTileIds">Set of tile IDs that are passable (optional)</param>
    /// <returns>SimpleMapData ready for rendering</returns>
    public SimpleMapData ToSimpleMapData(
        WfcGrid grid,
        BiomeDefinition biome,
        HashSet<string>? passableTileIds = null)
    {
        var width = grid.Width;
        var height = grid.Height;

        var mapData = new SimpleMapData
        {
            Size = new Vector2I(width, height),
            TileIds = new string[height, width],
            BiomeMap = new BiomeType[height, width]
        };

        var passablePositions = new List<Vector2I>();

        // Build passable set from biome if not provided
        passableTileIds ??= BuildPassableSet(biome);

        // Extract tile IDs from collapsed grid
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pos = new Vector2I(x, y);
                var cell = grid.GetCell(pos);

                if (!cell.IsCollapsed())
                {
                    // Use first available tile as fallback (shouldn't happen)
                    var firstTile = GetFirstTile(cell);
                    mapData.TileIds[y, x] = firstTile ?? "floor";
                }
                else
                {
                    mapData.TileIds[y, x] = cell.GetCollapsedTile();
                }

                mapData.BiomeMap[y, x] = biome.Type;

                // Track passable positions
                if (passableTileIds.Contains(mapData.TileIds[y, x]))
                {
                    passablePositions.Add(pos);
                }
            }
        }

        mapData.PassableTiles = passablePositions;

        // Set player start to first passable tile (or center if none)
        mapData.PlayerStart = passablePositions.Count > 0
            ? passablePositions[0]
            : new Vector2I(width / 2, height / 2);

        return mapData;
    }

    /// <summary>
    /// Converts grid with per-cell biome assignments.
    /// </summary>
    public SimpleMapData ToSimpleMapData(
        WfcGrid grid,
        BiomeType[,] biomeMap,
        HashSet<string> passableTileIds)
    {
        var width = grid.Width;
        var height = grid.Height;

        var mapData = new SimpleMapData
        {
            Size = new Vector2I(width, height),
            TileIds = new string[height, width],
            BiomeMap = biomeMap
        };

        var passablePositions = new List<Vector2I>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pos = new Vector2I(x, y);
                var cell = grid.GetCell(pos);

                mapData.TileIds[y, x] = cell.IsCollapsed()
                    ? cell.GetCollapsedTile()
                    : GetFirstTile(cell) ?? "floor";

                if (passableTileIds.Contains(mapData.TileIds[y, x]))
                {
                    passablePositions.Add(pos);
                }
            }
        }

        mapData.PassableTiles = passablePositions;
        mapData.PlayerStart = passablePositions.Count > 0
            ? passablePositions[0]
            : new Vector2I(width / 2, height / 2);

        return mapData;
    }

    private static HashSet<string> BuildPassableSet(BiomeDefinition biome)
    {
        var set = new HashSet<string>();

        if (biome.PassableTiles != null)
        {
            foreach (var tileId in biome.PassableTiles.GetAllTileIds())
            {
                set.Add(tileId);
            }
        }

        return set;
    }

    private static string? GetFirstTile(WfcCellState cell)
    {
        foreach (var tile in cell.GetPossibleTiles())
            return tile;
        return null;
    }
}
