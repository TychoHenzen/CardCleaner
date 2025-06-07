using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

public class SemanticWfcGenerator
{
    private readonly Array<SemanticTile> _tileSet;
    protected readonly Vector2I MapSize;
    protected readonly RandomNumberGenerator Rng;
    
    // The wave function - possible tiles at each position
    private List<SemanticTile>[][] _wave;
    private bool[][] _collapsed;
    
    public SemanticWfcGenerator(Array<SemanticTile> tileSet, Vector2I mapSize, ulong seed = 0)
    {
        _tileSet = tileSet;
        MapSize = mapSize;
        Rng = new RandomNumberGenerator();
        Rng.Seed = seed;
        
        InitializeWave();
    }
    
    private void InitializeWave()
    {
        _wave = new List<SemanticTile>[MapSize.Y][];
        _collapsed = new bool[MapSize.Y][];
        
        for (int y = 0; y < MapSize.Y; y++)
        {
            _wave[y] = new List<SemanticTile>[MapSize.X];
            _collapsed[y] = new bool[MapSize.X];
            
            for (int x = 0; x < MapSize.X; x++)
            {
                _wave[y][x] = new List<SemanticTile>(_tileSet);
                _collapsed[y][x] = false;
            }
        }
    }
    
    // Return SemanticTile[,] instead of int[,]
    public SemanticTile[,] Generate()
    {
        var result = new SemanticTile[MapSize.Y, MapSize.X];
        
        while (!IsFullyCollapsed())
        {
            var position = FindLowestEntropyCell();
            if (position.X == -1) break;
            
            CollapseCell(position);
            Propagate(position);
        }
        
        // Convert wave to SemanticTile array
        for (int y = 0; y < MapSize.Y; y++)
        {
            for (int x = 0; x < MapSize.X; x++)
            {
                if (_collapsed[y][x] && _wave[y][x].Count > 0)
                {
                    result[y, x] = _wave[y][x][0];
                }
                else if (_tileSet.Count > 0)
                {
                    result[y, x] = _tileSet[0]; // Fallback tile
                }
            }
        }
        
        return result;
    }
    
    // Rest of the methods are the same as WaveCollapseGenerator but work with SemanticTile
    private bool IsFullyCollapsed()
    {
        for (int y = 0; y < MapSize.Y; y++)
        {
            for (int x = 0; x < MapSize.X; x++)
            {
                if (!_collapsed[y][x]) return false;
            }
        }
        return true;
    }
    
    private Vector2I FindLowestEntropyCell()
    {
        int minEntropy = int.MaxValue;
        var candidates = new List<Vector2I>();
        
        for (int y = 0; y < MapSize.Y; y++)
        {
            for (int x = 0; x < MapSize.X; x++)
            {
                if (_collapsed[y][x]) continue;
                
                int entropy = _wave[y][x].Count;
                if (entropy == 0) return new Vector2I(-1, -1);
                
                if (entropy < minEntropy)
                {
                    minEntropy = entropy;
                    candidates.Clear();
                    candidates.Add(new Vector2I(x, y));
                }
                else if (entropy == minEntropy)
                {
                    candidates.Add(new Vector2I(x, y));
                }
            }
        }
        
        if (candidates.Count == 0) return new Vector2I(-1, -1);
        
        var chosen = candidates[Rng.RandiRange(0, candidates.Count - 1)];
        return chosen;
    }
    
    private void CollapseCell(Vector2I position)
    {
        if (_collapsed[position.Y][position.X] || _wave[position.Y][position.X].Count == 0) return;
        
        var chosenTile = ChooseWeightedTile(_wave[position.Y][position.X], position);
        _wave[position.Y][position.X].Clear();
        _wave[position.Y][position.X].Add(chosenTile);
        _collapsed[position.Y][position.X] = true;
    }
    
    protected virtual SemanticTile ChooseWeightedTile(List<SemanticTile> tiles, Vector2I position)
    {
        var totalWeight = tiles.Sum(t => t.BaseWeight);
        var randomValue = Rng.Randf() * totalWeight;
        
        float currentWeight = 0;
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
    
    private void Propagate(Vector2I start)
    {
        var stack = new Stack<Vector2I>();
        stack.Push(start);
        
        while (stack.Count > 0)
        {
            var position = stack.Pop();
            
            for (int dir = 0; dir < 4; dir++)
            {
                var direction = (Direction)dir;
                var neighbor = GetNeighborCoords(position, direction);
                
                if (!IsValidCoord(neighbor) || _collapsed[neighbor.Y][neighbor.X]) continue;
                
                var neighborWave = _wave[neighbor.Y][neighbor.X];
                var currentWave = _wave[position.Y][position.X];
                var tilesToRemove = new List<SemanticTile>();

                foreach (var neighborTile in neighborWave)
                {
                    bool canConnect = false;
                    
                    foreach (var currentTile in currentWave)
                    {
                        if (currentTile.CanConnectTo(neighborTile, direction))
                        {
                            canConnect = true;
                            break;
                        }
                    }
                    
                    if (!canConnect)
                    {
                        tilesToRemove.Add(neighborTile);
                    }
                }
                
                if (tilesToRemove.Count <= 0) 
                    continue;
                
                foreach (var tile in tilesToRemove)
                {
                    neighborWave.Remove(tile);
                }
                stack.Push(neighbor);
            }
        }
    }
    
    private static Vector2I GetNeighborCoords(Vector2I position, Direction direction)
    {
        return direction switch
        {
            Direction.North => new Vector2I(position.X, position.Y - 1),
            Direction.East => new Vector2I(position.X + 1, position.Y),
            Direction.South => new Vector2I(position.X, position.Y + 1),
            Direction.West => new Vector2I(position.X - 1, position.Y),
            _ => new Vector2I(position.X, position.Y)
        };
    }
    
    private bool IsValidCoord(Vector2I coord)
    {
        return coord.X >= 0 && coord.X < MapSize.X && coord.Y >= 0 && coord.Y < MapSize.Y;
    }
}
