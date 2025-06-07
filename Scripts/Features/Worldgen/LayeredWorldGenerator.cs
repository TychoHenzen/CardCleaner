using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

public partial class LayeredWorldGenerator
{
    private readonly Array<SemanticTile> _allTiles;
    private Vector2I _mapSize;
    private readonly RandomNumberGenerator _rng;
    private BaselineGradient _baselineGradient;


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

    public void Generate(ulong seed, TileMapLayer? terrainLayer, TileMapLayer? structureLayer, 
        TileMapLayer? decorationLayer, TileMapLayer? effectLayer, TileMapLayer? enemyLayer, 
        Vector2I mapSize, BaselineGradient? gradient = null, Array<EnemySpawnData>? enemies = null)
    {
        _rng.Seed = seed;
        _mapSize = mapSize;
        _baselineGradient = gradient;

        var terrainGrid = GenerateTerrainLayer();
        ApplyTilesToLayer(terrainGrid, terrainLayer);

        var structureGrid = GenerateLayerOnTop(terrainGrid, _structureTiles, _mapSize, _rng);
        ApplyTilesToLayer(structureGrid, structureLayer);

        var decorationGrid = GenerateLayerOnTop(structureGrid, _decorationTiles, _mapSize, _rng);
        ApplyTilesToLayer(decorationGrid, decorationLayer);

        var effectGrid = GenerateLayerOnTop(decorationGrid, _effectTiles, _mapSize, _rng);
        ApplyTilesToLayer(effectGrid, effectLayer);

        // Generate enemy layer
        if (enemies?.Count > 0)
        {
            var enemyGrid = GenerateEnemyLayer(terrainGrid, enemies, gradient);
            ApplyEnemyLayer(enemyGrid, enemyLayer);
        }

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
    
        // Use gradient-influenced generator if gradient provided
        if (_baselineGradient != null)
        {
            var gradientGenerator = new GradientInfluencedWfcGenerator(
                terrainTileArray, _mapSize, _rng.GetSeed(), _baselineGradient, 0.6f);
            return gradientGenerator.Generate();
        }
        else
        {
            var wfcGenerator = new SemanticWfcGenerator(terrainTileArray, _mapSize, _rng.GetSeed());
            return wfcGenerator.Generate();
        }
    }


    private EnemySpawnData?[,] GenerateEnemyLayer(SemanticTile?[,] terrainGrid,
        Array<EnemySpawnData> enemies, BaselineGradient? gradient)
    {
        var enemyGrid = new EnemySpawnData?[_mapSize.Y, _mapSize.X];
        var enemyList = enemies.ToList();

        for (int y = 0; y < _mapSize.Y; y++)
        {
            for (int x = 0; x < _mapSize.X; x++)
            {
                var terrain = terrainGrid[y, x];
                if (terrain == null) continue;

                // Calculate blended signature
                var position = new Vector2I(x, y);
                var baselineSignature = gradient?.GetSignatureAt(position, _mapSize) ?? new CardSignature();
                var terrainSignature = terrain.Signature ?? new CardSignature();
                var randomOffset = GenerateRandomSignatureOffset();

                var blendedSignature = BlendSignatures(baselineSignature, terrainSignature, randomOffset);

                // Find compatible enemies
                var compatibleEnemies = enemyList.Where(e => e.CanSpawnOnTile(terrain, blendedSignature)).ToList();
                if (compatibleEnemies.Count == 0) continue;

                // Select enemy by weight
                var selectedEnemy = SelectEnemyByWeight(compatibleEnemies, terrain, blendedSignature);
                if (selectedEnemy != null)
                {
                    enemyGrid[y, x] = selectedEnemy;
                }
            }
        }

        return enemyGrid;
    }

    private CardSignature BlendSignatures(CardSignature baseline, CardSignature terrain, CardSignature random)
    {
        var result = new CardSignature();
        for (int i = 0; i < 8; i++)
        {
            // Weighted blend: 40% baseline, 40% terrain, 20% random
            var blended = baseline[i] * 0.4f + terrain[i] * 0.4f + random[i] * 0.2f;
            result[i] = Mathf.Clamp(blended, -1f, 1f);
        }

        return result;
    }

    private CardSignature GenerateRandomSignatureOffset()
    {
        var result = new CardSignature();
        for (int i = 0; i < 8; i++)
        {
            result[i] = _rng.RandfRange(-0.3f, 0.3f); // Small random variation
        }

        return result;
    }

    private EnemySpawnData? SelectEnemyByWeight(List<EnemySpawnData> enemies,
        SemanticTile terrain, CardSignature signature)
    {
        var totalWeight = enemies.Sum(e => e.CalculateSpawnWeight(terrain, signature));
        if (totalWeight <= 0) return null;

        var randomValue = _rng.Randf() * totalWeight;
        var currentWeight = 0f;

        foreach (var enemy in enemies)
        {
            currentWeight += enemy.CalculateSpawnWeight(terrain, signature);
            if (randomValue <= currentWeight)
                return enemy;
        }

        return enemies.LastOrDefault();
    }

    private static void ApplyEnemyLayer(EnemySpawnData?[,] enemyGrid, TileMapLayer? layer)
    {
        if (layer == null) return;

        var height = enemyGrid.GetLength(0);
        var width = enemyGrid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var enemy = enemyGrid[y, x];
                if (enemy == null) continue;

                var position = new Vector2I(x, y);
                layer.SetCell(position, enemy.SourceId, enemy.AtlasCoords);
            }
        }
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

    private void PlacePattern(Vector2I position, SemanticTile pattern, SemanticTile?[,] layer, bool[,] occupied,
        Vector2I mapSize)
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

        // Check if any socket on the base tile is "Selected"
        if ((baseTile.Up & SocketType.Selected) != 0)
        {
            // If any socket has Selected flag, only the StackedTile is eligible
            if (baseTile.StackedTile != null)
            {
                compatible.Add(baseTile.StackedTile);
            }

            return compatible;
        }

        // Normal compatibility checking for non-Selected sockets
        foreach (var candidate in candidateTiles)
        {
            if (SemanticTile.SocketsCompatible(baseTile.Up, candidate.Down))
            {
                compatible.Add(candidate);
            }
        }

        return compatible;
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