using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

/// <summary>
/// Turns the two WFC layers of a grid map into the dual-grid overlays the terrain renderer draws: one entry per
/// visual tile, holding its base tile, its top auto-tile and its Corner16 bitmask.
/// </summary>
internal sealed class DualGridOverlayBuilder
{
    private readonly ITileRegistry _registry;
    private readonly string _defaultPassableTileId;

    public DualGridOverlayBuilder(ITileRegistry registry, string defaultPassableTileId)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _defaultPassableTileId = defaultPassableTileId ?? throw new ArgumentNullException(nameof(defaultPassableTileId));
    }

    /// <summary>
    /// The default passable tile id the map generator used: the registry's own default when it has one, else "floor".
    /// </summary>
    public static string ResolveDefaultPassableTileId(ITileRegistry registry)
    {
        // ASSUMPTION: this is the id GridMapGenerator handed to the generation context (metadata default, else
        // "floor"), so the renderer fills the same base tiles the generator filled.
        return registry is ITileMetadataProvider metadata
            ? metadata.GetDefaultPassableTileId() ?? "floor"
            : "floor";
    }

    public Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> Build(
        string[,] backgroundLayer,
        string[,] foregroundLayer,
        Vector2I size)
    {
        var visualWidth = size.X + 1;
        var visualHeight = size.Y + 1;
        var overlays = new Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)>();

        for (var vy = 0; vy < visualHeight; vy++)
        for (var vx = 0; vx < visualWidth; vx++)
        {
            var visualPosition = new Vector2I(vx, vy);
            var foregroundCorners = SampleCorners(foregroundLayer, size, vx, vy);
            var backgroundCorners = SampleCorners(backgroundLayer, size, vx, vy);
            var topTerrain = FindDominantAutoTile(foregroundCorners);
            var baseTerrain = FindDominantBaseTile(backgroundCorners);

            baseTerrain ??= _defaultPassableTileId;
            topTerrain ??= baseTerrain;

            var bitmask = ComputeTransitionBitmask(foregroundCorners, topTerrain);
            overlays[visualPosition] = (baseTerrain, topTerrain, bitmask);
        }

        return overlays;
    }

    private static string[] SampleCorners(string[,] grid, Vector2I size, int vx, int vy)
    {
        return new[]
        {
            GetCellSafe(grid, size, vx - 1, vy - 1),
            GetCellSafe(grid, size, vx, vy - 1),
            GetCellSafe(grid, size, vx - 1, vy),
            GetCellSafe(grid, size, vx, vy)
        };
    }

    private string? FindDominantAutoTile(IReadOnlyList<string> corners)
    {
        string? topTerrain = null;
        var topDominance = -1;

        foreach (var corner in corners)
        {
            if (string.IsNullOrEmpty(corner))
                continue;

            var tile = _registry.GetTile(corner);
            if (tile?.HasAutoTileVariants == true && tile.Dominance > topDominance)
            {
                topTerrain = corner;
                topDominance = tile.Dominance;
            }
        }

        return topTerrain;
    }

    private string? FindDominantBaseTile(IReadOnlyList<string> corners)
    {
        string? baseTerrain = null;
        var baseDominance = int.MaxValue;

        foreach (var corner in corners)
        {
            if (string.IsNullOrEmpty(corner))
                continue;

            var tile = _registry.GetTile(corner);
            if (tile != null && tile.Dominance < baseDominance)
            {
                baseTerrain = corner;
                baseDominance = tile.Dominance;
            }
        }

        return baseTerrain;
    }

    private int ComputeTransitionBitmask(IReadOnlyList<string> foregroundCorners, string topTerrain)
    {
        var bitmask = 0;
        if (_registry.AreSameTerrainType(foregroundCorners[1], topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthEast;
        if (_registry.AreSameTerrainType(foregroundCorners[3], topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthEast;
        if (_registry.AreSameTerrainType(foregroundCorners[2], topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthWest;
        if (_registry.AreSameTerrainType(foregroundCorners[0], topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthWest;
        return bitmask;
    }

    private static string GetCellSafe(string[,] grid, Vector2I size, int x, int y)
    {
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);
        return grid[y, x];
    }
}
