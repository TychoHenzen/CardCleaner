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

    private string[] SampleCorners(string[,] grid, Vector2I size, int vx, int vy)
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

    private string? SelectSimpleTileFromBiome(BiomeDefinition biome)
    {
        foreach (var tile in _context.TileRegistry.GetAllTiles())
        {
            if (tile.IsPassable && !tile.HasAutoTileVariants && tile.IsAllowedInBiome(biome.Id))
                return tile.Id;
        }

        return null;
    }

    private TerrainSelection SelectTerrains(
        Dictionary<string, (int Dominance, bool HasAutoTile)> terrainInfo)
    {
        string? baseTerrain = null;
        string? topTerrain = null;
        var baseDominance = int.MaxValue;
        var topDominance = -1;

        foreach (var (terrain, (dominance, hasAutoTile)) in terrainInfo)
        {
            if (hasAutoTile)
            {
                SelectTopTerrain(terrain, dominance, ref topTerrain, ref topDominance);
            }
            else
            {
                SelectBaseTerrain(terrain, dominance, ref baseTerrain, ref baseDominance);
            }
        }

        topTerrain ??= baseTerrain ?? _context.DefaultPassableTileId;
        baseTerrain ??= topTerrain;
        return new TerrainSelection(baseTerrain, topTerrain);
    }

    private static void SelectTopTerrain(
        string terrain,
        int dominance,
        ref string? selectedTerrain,
        ref int selectedDominance)
    {
        if (!ShouldReplaceTopTerrain(selectedTerrain, selectedDominance, terrain, dominance))
            return;

        selectedTerrain = terrain;
        selectedDominance = dominance;
    }

    private static void SelectBaseTerrain(
        string terrain,
        int dominance,
        ref string? selectedTerrain,
        ref int selectedDominance)
    {
        if (!ShouldReplaceBaseTerrain(selectedTerrain, selectedDominance, terrain, dominance))
            return;

        selectedTerrain = terrain;
        selectedDominance = dominance;
    }

    private static bool ShouldReplaceTopTerrain(
        string? currentTerrain,
        int currentDominance,
        string candidate,
        int candidateDominance)
    {
        return currentTerrain == null ||
            candidateDominance > currentDominance ||
            (candidateDominance == currentDominance &&
             string.CompareOrdinal(candidate, currentTerrain) < 0);
    }

    private static bool ShouldReplaceBaseTerrain(
        string? currentTerrain,
        int currentDominance,
        string candidate,
        int candidateDominance)
    {
        return currentTerrain == null ||
            candidateDominance < currentDominance ||
            (candidateDominance == currentDominance &&
             string.CompareOrdinal(candidate, currentTerrain) < 0);
    }

    private static int ComputeBitmask(
        string[,] terrainGrid,
        Vector2I size,
        int vx,
        int vy,
        string topTerrain)
    {
        var bitmask = 0;
        if (IsTerrainAtPosition(terrainGrid, size, vx, vy - 1, topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthEast;
        if (IsTerrainAtPosition(terrainGrid, size, vx, vy, topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthEast;
        if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy, topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthWest;
        if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy - 1, topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthWest;
        return bitmask;
    }

    private void SampleTerrainCell(
        string[,] terrainGrid,
        Vector2I size,
        int x,
        int y,
        Dictionary<string, (int Dominance, bool HasAutoTile)> terrainInfo)
    {
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);

        var tileId = terrainGrid[y, x];
        var tile = _context.TileRegistry.GetTile(tileId);
        if (tile == null || tile.Layer != TileLayer.Terrain)
            return;

        if (!terrainInfo.ContainsKey(tileId))
            terrainInfo[tileId] = (tile.Dominance, tile.HasAutoTileVariants);
    }

    private static bool IsTerrainAtPosition(
        string[,] terrainGrid,
        Vector2I size,
        int x,
        int y,
        string terrainTileId)
    {
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);
        return terrainGrid[y, x] == terrainTileId;
    }
}
