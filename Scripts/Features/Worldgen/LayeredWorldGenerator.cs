using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// Generates layered worlds using socket-based compatibility and probabilistic placement
/// </summary>
public partial class LayeredWorldGenerator
{
    private readonly Array<SemanticTile> _allTiles;
    private Vector2I _mapSize;
    private readonly RandomNumberGenerator _rng;
    
    // Cached tile lists by layer for performance
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
        
        // Phase 1: Generate terrain using WFC (horizontal constraints only)
        var terrainGrid = GenerateTerrainLayer();
        ApplyTilesToLayer(terrainGrid, terrainLayer);
        
        // Phase 2: Place structures based on terrain sockets and GlobalSpawnChance
        var structureGrid = GenerateLayerOnTop(terrainGrid, _structureTiles);
        ApplyTilesToLayer(structureGrid, structureLayer);
        
        // Phase 3: Place decorations on structures
        var decorationGrid = GenerateLayerOnTop(structureGrid, _decorationTiles);
        ApplyTilesToLayer(decorationGrid, decorationLayer);
        
        // Phase 4: Place effects on decorations
        var effectGrid = GenerateLayerOnTop(decorationGrid, _effectTiles);
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
        
        // Use existing SemanticWfcGenerator for terrain (horizontal constraints only)
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

    private void PlacePattern(Vector2I position, SemanticTile pattern, SemanticTile?[,] layer, bool[,] occupied)
    {
        // Check if pattern fits
        if (!CanPlacePattern(position, pattern, occupied)) return;
    
        // Place pattern tiles and mark as occupied
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                var worldPos = position + new Vector2I(px, py);
                if (worldPos.X >= _mapSize.X || worldPos.Y >= _mapSize.Y) continue;
            
                var tilePlacement = pattern.GetTileAt(new Vector2I(px, py));
                if (tilePlacement != null)
                {
                    // Place the pattern tile - each tile can spawn decorations in next layer
                    layer[worldPos.Y, worldPos.X] = pattern; // Use same pattern for socket compatibility
                
                    if (pattern.ShouldBlockAt(new Vector2I(px, py)))
                    {
                        occupied[worldPos.Y, worldPos.X] = true;
                    }
                }
            }
        }
    }
    private bool CanPlacePattern(Vector2I position, SemanticTile pattern, bool[,] occupied)
    {
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                var checkPos = position + new Vector2I(px, py);
                if (checkPos.X >= _mapSize.X || checkPos.Y >= _mapSize.Y || 
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
            // Check socket compatibility: base tile's Up socket must match candidate's Down socket
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
    
    private SemanticTile? SelectTileByWeight(List<SemanticTile> tiles)
    {
        if (tiles.Count == 0) return null;
        if (tiles.Count == 1) return tiles[0];
        
        var totalWeight = tiles.Sum(t => t.BaseWeight);
        if (totalWeight <= 0) return tiles[0]; // Fallback to first tile
        
        var randomValue = _rng.Randf() * totalWeight;
        var currentWeight = 0f;
        
        foreach (var tile in tiles)
        {
            currentWeight += tile.BaseWeight;
            if (randomValue <= currentWeight)
            {
                return tile;
            }
        }
        
        return tiles[^1]; // Fallback to last tile
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
            
                // Find which tile within the pattern this position represents
                // This handles both 1x1 and multi-tile patterns uniformly
                var tilePlacement = pattern.GetTileAt(new Vector2I(0, 0)); // For now, use first tile
            
                if (tilePlacement != null)
                {
                    layer.SetCell(position, tilePlacement.SourceId, tilePlacement.AtlasCoords);
                }
            }
        }
    }

}