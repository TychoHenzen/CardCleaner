using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

public partial class TwoPhaseWorldGenerator : Node2D
{
    [Export] public TileMap[] TileLayers { get; set; } = new TileMap[4]; // One for each TileLayer
    [Export] public Array<SemanticTile> SemanticTiles { get; set; } = new();
    
    private SemanticTile[,] _semanticMap;
    private bool[,] _occupiedTiles;
    
    public void GenerateWorld(Vector2I mapSize, uint seed = 0)
    {
        // Phase 1: Generate semantic tile layout using WFC
        var wfcGenerator = new SemanticWfcGenerator(SemanticTiles, mapSize, seed);
        _semanticMap = wfcGenerator.Generate();
        
        // Phase 2: Place terrain and tile patterns
        _occupiedTiles = new bool[mapSize.Y, mapSize.X];
        PlaceBaseTerrain();
        PlaceTilePatterns();
    }
    
    private void PlaceBaseTerrain()
    {
        var terrainLayer = TileLayers[(int)TileLayer.Terrain];
        if (terrainLayer == null) return;
        
        for (int y = 0; y < _semanticMap.GetLength(0); y++)
        {
            for (int x = 0; x < _semanticMap.GetLength(1); x++)
            {
                var semanticTile = _semanticMap[y, x];
                if (semanticTile?.SpriteRegion == null)
                    continue;
                
                // Use the first sprite region for base terrain
                var baseSprite = semanticTile.SpriteRegion;
                if (!(baseSprite?.Layers.Length > 0)) 
                    continue;
                
                var tileRef = baseSprite.Layers[0];
                terrainLayer.SetCell(0, new Vector2I(x, y), 
                    tileRef.SourceId,
                    tileRef.AtlasCoords);
            }
        }
    }
    private void PlaceTilePatterns()
    {
        var rng = new RandomNumberGenerator();
    
        for (int y = 0; y < _semanticMap.GetLength(0); y++)
        {
            for (int x = 0; x < _semanticMap.GetLength(1); x++)
            {
                if (_occupiedTiles[y, x]) continue;
            
                var semanticTile = _semanticMap[y, x];
                if (semanticTile?.SpawnPatterns == null || semanticTile.SpawnPatterns.Length == 0) continue;
            
                // First: Check if ANY pattern should spawn here
                if (rng.Randf() > semanticTile.GlobalSpawnChance) continue;
            
                // Second: Weight-select which pattern to spawn
                var selectedPattern = SelectPatternByWeight(semanticTile.SpawnPatterns, rng);
                if (selectedPattern == null) continue;
            
                if (CanPlacePattern(new Vector2I(x, y), selectedPattern))
                {
                    PlacePattern(new Vector2I(x, y), selectedPattern);
                    MarkTilesOccupiedSelective(new Vector2I(x, y), selectedPattern);
                    break;
                }
            }
        }
    }

    private TilePattern SelectPatternByWeight(TilePattern[] patterns, RandomNumberGenerator rng)
    {
        float totalWeight = patterns.Sum(p => p.Weight);
        if (totalWeight <= 0) return null;
    
        float random = rng.Randf() * totalWeight;
        float currentWeight = 0;
    
        foreach (var pattern in patterns)
        {
            currentWeight += pattern.Weight;
            if (random <= currentWeight)
                return pattern;
        }
    
        return patterns[^1]; // Fallback to last pattern
    }

    private void MarkTilesOccupiedSelective(Vector2I position, TilePattern pattern)
    {
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                if (pattern.ShouldBlockAt(new Vector2I(px, py)))
                {
                    var markPos = position + new Vector2I(px, py);
                    if (markPos.X < _occupiedTiles.GetLength(1) && 
                        markPos.Y < _occupiedTiles.GetLength(0))
                    {
                        _occupiedTiles[markPos.Y, markPos.X] = true;
                    }
                }
            }
        }
    }
    
    private bool CanPlacePattern(Vector2I position, TilePattern pattern)
    {
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                var checkPos = position + new Vector2I(px, py);
                if (checkPos.X >= _occupiedTiles.GetLength(1) || 
                    checkPos.Y >= _occupiedTiles.GetLength(0) ||
                    _occupiedTiles[checkPos.Y, checkPos.X])
                {
                    return false;
                }
            }
        }
        return true;
    }
    
    private void PlacePattern(Vector2I position, TilePattern pattern)
    {
        var targetLayer = TileLayers[(int)pattern.Layer];
        if (targetLayer == null) return;
        
        for (int py = 0; py < pattern.Size.Y; py++)
        {
            for (int px = 0; px < pattern.Size.X; px++)
            {
                var tilePlacement = pattern.GetTileAt(new Vector2I(px, py));
                if (tilePlacement == null) continue; // Empty spot in pattern
                
                var worldPos = position + new Vector2I(px, py);
                targetLayer.SetCell(0, worldPos, 
                    tilePlacement.SourceId, 
                    tilePlacement.AtlasCoords);
                
                // Handle animation if needed
                if (tilePlacement.AnimationFrames.Count > 1)
                {
                    SetupTileAnimation(targetLayer, worldPos, tilePlacement);
                }
            }
        }
    }
    
    private void SetupTileAnimation(TileMap tileMap, Vector2I position, TilePlacement tilePlacement)
    {
        // You can implement tile animation here using Godot's built-in tile animation
        // or create a custom animation system that swaps atlas coordinates over time
        // For now, just place the first frame
        tileMap.SetCell(0, position, tilePlacement.SourceId, tilePlacement.AnimationFrames[0]);
    }
    
    private void MarkTilesOccupied(Vector2I position, Vector2I size)
    {
        for (int py = 0; py < size.Y; py++)
        {
            for (int px = 0; px < size.X; px++)
            {
                var markPos = position + new Vector2I(px, py);
                if (markPos.X < _occupiedTiles.GetLength(1) && 
                    markPos.Y < _occupiedTiles.GetLength(0))
                {
                    _occupiedTiles[markPos.Y, markPos.X] = true;
                }
            }
        }
    }
    
}