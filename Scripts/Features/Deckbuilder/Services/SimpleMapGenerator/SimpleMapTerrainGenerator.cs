using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.SimpleMapGeneratorSupport;

internal sealed class SimpleMapTerrainGenerator
{
    private readonly SimpleMapGenerationContext _context;

    public SimpleMapTerrainGenerator(SimpleMapGenerationContext context)
    {
        _context = context;
    }

    public Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)>
        GenerateTerrainTransitions(
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

            baseTerrain ??= _context.DefaultPassableTileId;
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

            var tile = _context.TileRegistry.GetTile(corner);
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

            var tile = _context.TileRegistry.GetTile(corner);
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
        if (_context.TileRegistry.AreSameTerrainType(foregroundCorners[1], topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthEast;
        if (_context.TileRegistry.AreSameTerrainType(foregroundCorners[3], topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthEast;
        if (_context.TileRegistry.AreSameTerrainType(foregroundCorners[2], topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthWest;
        if (_context.TileRegistry.AreSameTerrainType(foregroundCorners[0], topTerrain))
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
