using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public class WaveCollapseGenerator
    {
        private readonly List<WfcTile> _tileSet;
        private Dictionary<(SocketType, SocketType), bool> _socketCompatibility;
        private readonly Vector2I _mapSize;
        private readonly RandomNumberGenerator _rng;
        
        // The wave function - possible tiles at each position
        private List<WfcTile>[][] _wave;
        private bool[][] _collapsed;
        
        public WaveCollapseGenerator(List<WfcTile> tileSet, Vector2I mapSize, uint seed = 0)
        {
            _tileSet = tileSet;
            _mapSize = mapSize;
            _rng = new RandomNumberGenerator();
            _rng.Seed = seed;
            
            InitializeWave();
            BuildCompatibilityTable();
        }
        
        private void InitializeWave()
        {
            _wave = new List<WfcTile>[_mapSize.Y][];
            _collapsed = new bool[_mapSize.Y][];
            
            for (int y = 0; y < _mapSize.Y; y++)
            {
                _wave[y] = new List<WfcTile>[_mapSize.X];
                _collapsed[y] = new bool[_mapSize.X];
                
                for (int x = 0; x < _mapSize.X; x++)
                {
                    // Initially, all tiles are possible at every position
                    _wave[y][x] = new List<WfcTile>(_tileSet);
                    _collapsed[y][x] = false;
                }
            }
        }
        
        private void BuildCompatibilityTable()
        {
            _socketCompatibility = new Dictionary<(SocketType, SocketType), bool>();
            
            // Build compatibility table for all socket type pairs
            var socketTypes = Enum.GetValues<SocketType>();
            foreach (var socket1 in socketTypes)
            {
                foreach (var socket2 in socketTypes)
                {
                    _socketCompatibility[(socket1, socket2)] = GetSocketCompatibility(socket1, socket2);
                }
            }
        }
        
        private static bool GetSocketCompatibility(SocketType socket1, SocketType socket2)
        {
            // Same sockets always connect
            if (socket1 == socket2) return true;
            
            // Mixed connects to everything
            if (socket1 == SocketType.Any || socket2 == SocketType.Any) return true;
            // Define specific rules here
            // For now, only same types and mixed types connect
            return false;
        }
        
        public int[,] Generate()
        {
            var result = new int[_mapSize.Y, _mapSize.X];
            
            while (!IsFullyCollapsed())
            {
                var position = FindLowestEntropyCell();
                if (position.X == -1) break; // No valid cell found
                
                CollapseCell(position);
                Propagate(position);
            }
            
            // Convert wave to tile IDs
            for (int y = 0; y < _mapSize.Y; y++)
            {
                for (int x = 0; x < _mapSize.X; x++)
                {
                    if (_collapsed[y][x] && _wave[y][x].Count > 0)
                    {
                        result[y, x] = _wave[y][x][0].AtlasId;
                    }
                    else
                    {
                        result[y, x] = 0; // Fallback tile
                    }
                }
            }
            
            return result;
        }
        
        private bool IsFullyCollapsed()
        {
            for (int y = 0; y < _mapSize.Y; y++)
            {
                for (int x = 0; x < _mapSize.X; x++)
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
            
            for (int y = 0; y < _mapSize.Y; y++)
            {
                for (int x = 0; x < _mapSize.X; x++)
                {
                    if (_collapsed[y][x]) continue;
                    
                    int entropy = _wave[y][x].Count;
                    if (entropy == 0) return new Vector2I(-1, -1); // Impossible state
                    
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
            
            // Randomly pick from cells with lowest entropy
            var chosen = candidates[_rng.RandiRange(0, candidates.Count - 1)];
            return chosen;
        }
        
        private void CollapseCell(Vector2I position)
        {
            if (_collapsed[position.Y][position.X] || _wave[position.Y][position.X].Count == 0) return;
            
            // Choose tile based on weighted probability
            var chosenTile = ChooseWeightedTile(_wave[position.Y][position.X]);
            _wave[position.Y][position.X].Clear();
            _wave[position.Y][position.X].Add(chosenTile);
            _collapsed[position.Y][position.X] = true;
        }
        
        private WfcTile ChooseWeightedTile(List<WfcTile> tiles)
        {
            // Simple weighted selection - can be enhanced with signature influence
            var totalWeight = tiles.Sum(t => t.BaseWeight);
            var randomValue = _rng.Randf() * totalWeight;
            
            float currentWeight = 0;
            foreach (var tile in tiles)
            {
                currentWeight += tile.BaseWeight;
                if (randomValue <= currentWeight)
                {
                    return tile;
                }
            }
            
            return tiles[^1]; // Fallback
        }
        
        private void Propagate(Vector2I start)
        {
            var stack = new Stack<Vector2I>();
            stack.Push(start);
            
            while (stack.Count > 0)
            {
                var position = stack.Pop();
                
                // Check all four directions
                for (int dir = 0; dir < 4; dir++)
                {
                    var direction = (Direction)dir;
                    var neighbor = GetNeighborCoords(position, direction);
                    
                    if (!IsValidCoord(neighbor) || _collapsed[neighbor.Y][neighbor.X]) continue;
                    
                    // Remove incompatible tiles from neighbor
                    var neighborWave = _wave[neighbor.Y][neighbor.X];
                    var currentWave = _wave[position.Y][position.X];
                    var tilesToRemove = new List<WfcTile>();

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
                    
                    // If we removed any tiles, propagate further
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
            return coord.X >= 0 && coord.X < _mapSize.X && coord.Y >= 0 && coord.Y < _mapSize.Y;
        }
    }
