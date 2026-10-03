using System;
using System.Collections.Generic;
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
    /// <param name="passableTileIds">Set of tile IDs that are passable (required)</param>
    /// <param name="defaultTileId">Optional fallback tile ID; null uses the first available.</param>
    /// <returns>SimpleMapData ready for rendering</returns>
    public SimpleMapData ToSimpleMapData(
        WfcGrid grid,
        BiomeDefinition biome,
        HashSet<string>? passableTileIds = null,
        string? defaultTileId = null)
    {
        if (passableTileIds == null)
            throw new ArgumentNullException(nameof(passableTileIds),
                "passableTileIds must be provided. " +
                "Use TileDefinition.IsPassable to build the set from a tile registry.");

        var width = grid.Width;
        var height = grid.Height;

        var mapData = new SimpleMapData
        {
            Size = new Vector2I(width, height),
            TileIds = new string[height, width],
            BiomeMap = new string[height, width]
        };

        var passablePositions = new List<Vector2I>();

        // Extract tile IDs from collapsed grid
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pos = new Vector2I(x, y);
                var cell = grid.GetCell(pos);

                if (!cell.IsCollapsed())
                {
                    // Use first available tile as fallback (shouldn't happen in well-formed grids)
                    var firstTile = GetFirstTile(cell);
                    mapData.TileIds[y, x] = firstTile ?? defaultTileId ?? string.Empty;
                }
                else
                {
                    mapData.TileIds[y, x] = cell.GetCollapsedTile();
                }

                mapData.BiomeMap[y, x] = biome.Id;

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
    /// <param name="grid">Fully collapsed WFC grid</param>
    /// <param name="biomeMap">Per-cell biome assignments</param>
    /// <param name="passableTileIds">Set of tile IDs that are passable</param>
    /// <param name="defaultTileId">Optional fallback tile ID; null uses the first available.</param>
    public SimpleMapData ToSimpleMapData(
        WfcGrid grid,
        string[,] biomeMap,
        HashSet<string> passableTileIds,
        string? defaultTileId = null)
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
                    : GetFirstTile(cell) ?? defaultTileId ?? string.Empty;

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

    private static string? GetFirstTile(WfcCellState cell)
    {
        foreach (var tile in cell.GetPossibleTiles())
            return tile;
        return null;
    }
}
