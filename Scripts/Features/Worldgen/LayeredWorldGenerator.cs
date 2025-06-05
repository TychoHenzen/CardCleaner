using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

public partial class LayeredWorldGenerator
{
    private readonly Array<SemanticTile> _allTiles;
    private Vector2I _mapSize;
    private readonly RandomNumberGenerator _rng;
    
    private readonly List<SemanticTile> _terrainTiles = new();
    private readonly List<SemanticTile> _structureTiles = new();
    private readonly List<SemanticTile> _decorationTiles = new();
    private readonly List<SemanticTile> _effectTiles = new();
    
    public LayeredWorldGenerator(Array<SemanticTile> semanticTiles)
    {
        _allTiles = semanticTiles;
        _rng = new RandomNumberGenerator();
        CacheTilesByLayer();
    }
    
    private void CacheTilesByLayer()
    {
        foreach (var tile in _allTiles)
        {
            switch (tile.Layer)
            {
                case TileLayer.Terrain:
                    _terrainTiles.Add(tile);
                    break;
                case TileLayer.Structure:
                    _structureTiles.Add(tile);
                    break;
                case TileLayer.Decoration:
                    _decorationTiles.Add(tile);
                    break;
                case TileLayer.Effects:
                    _effectTiles.Add(tile);
                    break;
            }
        }
    }
    
    public void Generate(uint seed, TileMapLayer? terrainLayer, TileMapLayer? structureLayer, 
        TileMapLayer? decorationLayer, TileMapLayer? effectLayer, Vector2I mapSize)
    {
        _rng.Seed = seed;
        _mapSize = mapSize;
        
        var terrainGrid = GenerateTerrainLayer();
        ApplyTilesToLayer(terrainGrid, terrainLayer);
        
        var structureGrid = GenerateLayerOnTop(terrainGrid, _structureTiles, _mapSize, _rng);
        ApplyTilesToLayer(structureGrid, structureLayer);
        
        var decorationGrid = GenerateLayerOnTop(structureGrid, _decorationTiles, _mapSize, _rng);
        ApplyTilesToLayer(decorationGrid, decorationLayer);
        
        var effectGrid = GenerateLayerOnTop(decorationGrid, _effectTiles, _mapSize, _rng);
        ApplyTilesToLayer(effectGrid, effectLayer);
        
        ILog.Print($"Generated layered world with {_mapSize.X}x{_mapSize.Y} tiles");
    }
    
    private SemanticTile?[,] GenerateTerrainLayer()
    {
        if (_terrainTiles.Count == 0)
        {
            ILog.Warning("No terrain tiles available for generation");
            return new SemanticTile?[_mapSize.Y, _mapSize.X];
        }
        
        var terrainTileArray = new Array<SemanticTile>();
        foreach (var tile in _terrainTiles)
        {
            terrainTileArray.Add(tile);
        }
        
        var wfcGenerator = new SemanticWfcGenerator(terrainTileArray, _mapSize, _rng.GetSeed());
        return wfcGenerator.Generate();
    }

    public SemanticTile?[,] GenerateLayerOnTop(SemanticTile?[,] baseLayer, List<SemanticTile> candidateTiles, 
        Vector2I mapSize, RandomNumberGenerator rng)
    {
        var newLayer = new SemanticTile?[mapSize.Y, mapSize.X];
        var occupiedTiles = new bool[mapSize.Y, mapSize.X];
    
        if (candidateTiles.Count == 0) return newLayer;
    
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                if (occupiedTiles[y, x]) continue;
            
                var baseTile = baseLayer[y, x];
                if (baseTile == null) continue;
            
                if (rng.Randf() > baseTile.GlobalSpawnChance) continue;
            
                var compatibleTiles = GetCompatibleTiles(baseTile, candidateTiles);
                if (compatibleTiles.Count == 0) continue;
            
                var selectedTile = SelectTileByWeight(compatibleTiles, rng);
                if (selectedTile == null) continue;
            
                PlacePattern(new Vector2I(x, y), selectedTile, newLayer, occupiedTiles, mapSize);
            }
        }
    
        return newLayer;
    }

    private void PlacePattern(Vector2I position, SemanticTile pattern, SemanticTile?[,] layer, bool[,] occupied, Vector2I mapSize)
    {
        if (!CanPlacePattern(position, pattern, occupied, mapSize)) return;
    
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                var worldPos = position + new Vector2I(px, py);
                if (worldPos.X >= mapSize.X || worldPos.Y >= mapSize.Y) continue;
            
                var tilePlacement = pattern.GetTileAt(new Vector2I(px, py));
                if (tilePlacement != null)
                {
                    layer[worldPos.Y, worldPos.X] = pattern;
                
                    if (pattern.ShouldBlockAt(new Vector2I(px, py)))
                    {
                        occupied[worldPos.Y, worldPos.X] = true;
                    }
                }
            }
        }
    }
    
    private bool CanPlacePattern(Vector2I position, SemanticTile pattern, bool[,] occupied, Vector2I mapSize)
    {
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                var checkPos = position + new Vector2I(px, py);
                if (checkPos.X >= mapSize.X || checkPos.Y >= mapSize.Y || 
                    occupied[checkPos.Y, checkPos.X])
                {
                    return false;
                }
            }
        }
        return true;
    }
    
    private List<SemanticTile> GetCompatibleTiles(SemanticTile baseTile, List<SemanticTile> candidateTiles)
    {
        var compatible = new List<SemanticTile>();
        
        foreach (var candidate in candidateTiles)
        {
            if (SocketsCompatible(baseTile.Up, candidate.Down))
            {
                compatible.Add(candidate);
            }
        }
        
        return compatible;
    }
    
    private static bool SocketsCompatible(SocketType socket1, SocketType socket2)
    {
        if (socket1 == socket2) return true;
        if (socket1 == SocketType.Any || socket2 == SocketType.Any) return true;
        return false;
    }
    
    private SemanticTile? SelectTileByWeight(List<SemanticTile> tiles, RandomNumberGenerator rng)
    {
        if (tiles.Count == 0) return null;
        if (tiles.Count == 1) return tiles[0];
        
        var totalWeight = tiles.Sum(t => t.BaseWeight);
        if (totalWeight <= 0) return tiles[0];
        
        var randomValue = rng.Randf() * totalWeight;
        var currentWeight = 0f;
        
        foreach (var tile in tiles)
        {
            currentWeight += tile.BaseWeight;
            if (randomValue <= currentWeight)
            {
                return tile;
            }
        }
        
        return tiles[^1];
    }
    
    private static void ApplyTilesToLayer(SemanticTile?[,] tileGrid, TileMapLayer? layer)
    {
        if (layer == null) return;
    
        var height = tileGrid.GetLength(0);
        var width = tileGrid.GetLength(1);
    
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pattern = tileGrid[y, x];
                if (pattern == null) continue;
            
                var position = new Vector2I(x, y);
                var tilePlacement = pattern.GetTileAt(new Vector2I(0, 0));
            
                if (tilePlacement != null)
                {
                    layer.SetCell(position, tilePlacement.SourceId, tilePlacement.AtlasCoords);
                }
            }
        }
    }
}