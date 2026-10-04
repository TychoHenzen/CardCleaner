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

        var biomeMap = new string[grid.Height, grid.Width];
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                biomeMap[y, x] = biome.Id;
            }
        }

        return BuildMapData(grid, biomeMap, passableTileIds, defaultTileId);
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
        return BuildMapData(grid, biomeMap, passableTileIds, defaultTileId);
    }

    private static SimpleMapData BuildMapData(
        WfcGrid grid,
        string[,] biomeMap,
        HashSet<string> passableTileIds,
        string? defaultTileId)
    {
        var width = grid.Width;
        var height = grid.Height;

        var mapData = new SimpleMapData
        {
            Size = new Vector2I(width, height),
            TileIds = new string[height, width],
            BiomeMap = biomeMap
        };

        var passablePositions = FillTileIds(grid, mapData.TileIds, passableTileIds, defaultTileId);

        mapData.PassableTiles = passablePositions;

        // Set player start to first passable tile (or center if none)
        mapData.PlayerStart = passablePositions.Count > 0
            ? passablePositions[0]
            : new Vector2I(width / 2, height / 2);

        return mapData;
    }

    /// <summary>
    /// Copies each cell tile into tileIds and returns the positions whose tile is passable.
    /// An uncollapsed cell (not expected in a well-formed grid) uses its first possible tile.
    /// </summary>
    private static List<Vector2I> FillTileIds(
        WfcGrid grid,
        string[,] tileIds,
        HashSet<string> passableTileIds,
        string? defaultTileId)
    {
        var passablePositions = new List<Vector2I>();

        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var pos = new Vector2I(x, y);
                var cell = grid.GetCell(pos);

                tileIds[y, x] = cell.IsCollapsed()
                    ? cell.GetCollapsedTile()
                    : GetFirstTile(cell) ?? defaultTileId ?? string.Empty;

                if (passableTileIds.Contains(tileIds[y, x]))
                    passablePositions.Add(pos);
            }
        }

        return passablePositions;
    }

    private static string? GetFirstTile(WfcCellState cell)
    {
        foreach (var tile in cell.GetPossibleTiles())
            return tile;
        return null;
    }
}
