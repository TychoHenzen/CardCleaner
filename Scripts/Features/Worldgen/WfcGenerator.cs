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
            if (socket1 == SocketType.Mixed || socket2 == SocketType.Mixed) return true;
            
            // Define specific rules here
            // For now, only same types and mixed types connect
            return false;
        }
        
        public int[,] Generate()
        {
            var result = new int[_mapSize.Y, _mapSize.X];
            
            while (!IsFullyCollapsed())
            {
                var (x, y) = FindLowestEntropyCell();
                if (x == -1) break; // No valid cell found
                
                CollapseCell(x, y);
                Propagate(x, y);
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
        
        private (int x, int y) FindLowestEntropyCell()
        {
            int minEntropy = int.MaxValue;
            var candidates = new List<(int x, int y)>();
            
            for (int y = 0; y < _mapSize.Y; y++)
            {
                for (int x = 0; x < _mapSize.X; x++)
                {
                    if (_collapsed[y][x]) continue;
                    
                    int entropy = _wave[y][x].Count;
                    if (entropy == 0) return (-1, -1); // Impossible state
                    
                    if (entropy < minEntropy)
                    {
                        minEntropy = entropy;
                        candidates.Clear();
                        candidates.Add((x, y));
                    }
                    else if (entropy == minEntropy)
                    {
                        candidates.Add((x, y));
                    }
                }
            }
            
            if (candidates.Count == 0) return (-1, -1);
            
            // Randomly pick from cells with lowest entropy
            var chosen = candidates[_rng.RandiRange(0, candidates.Count - 1)];
            return chosen;
        }
        
        private void CollapseCell(int x, int y)
        {
            if (_collapsed[y][x] || _wave[y][x].Count == 0) return;
            
            // Choose tile based on weighted probability
            var chosenTile = ChooseWeightedTile(_wave[y][x]);
            _wave[y][x].Clear();
            _wave[y][x].Add(chosenTile);
            _collapsed[y][x] = true;
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
            
            return tiles.Last(); // Fallback
        }
        
        private void Propagate(int startX, int startY)
        {
            var stack = new Stack<(int x, int y)>();
            stack.Push((startX, startY));
            
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                
                // Check all four directions
                for (int dir = 0; dir < 4; dir++)
                {
                    var direction = (Direction)dir;
                    var (nx, ny) = GetNeighborCoords(x, y, direction);
                    
                    if (!IsValidCoord(nx, ny) || _collapsed[ny][nx]) continue;
                    
                    // Remove incompatible tiles from neighbor
                    var neighborWave = _wave[ny][nx];
                    var currentWave = _wave[y][x];
                    
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
                    if (tilesToRemove.Count > 0)
                    {
                        foreach (var tile in tilesToRemove)
                        {
                            neighborWave.Remove(tile);
                        }
                        stack.Push((nx, ny));
                    }
                }
            }
        }
        
        private (int x, int y) GetNeighborCoords(int x, int y, Direction direction)
        {
            return direction switch
            {
                Direction.North => (x, y - 1),
                Direction.East => (x + 1, y),
                Direction.South => (x, y + 1),
                Direction.West => (x - 1, y),
                _ => (x, y)
            };
        }
        
        private bool IsValidCoord(int x, int y)
        {
            return x >= 0 && x < _mapSize.X && y >= 0 && y < _mapSize.Y;
        }
    }
