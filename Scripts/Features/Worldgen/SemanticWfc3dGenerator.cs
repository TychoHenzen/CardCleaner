using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enum;
using Godot;


namespace CardCleaner.Scripts.Features.Worldgen;

public class SemanticWfc3dGenerator
{
    private readonly SemanticTile[] _tileSet;
    private readonly Vector3I _mapSize; // X, Y, Z (where Z = number of layers)
    private readonly TileLayer[] _layerOrder; // [Terrain, Decoration, Structure, Effects]
    private readonly RandomNumberGenerator _rng;
    private readonly ConstraintManager _constraintManager = new();

    // 3D wave function: [layer][y][x]
    private List<SemanticTile>[][][] _wave;
    private bool[][][] _collapsed;

    // Precomputed tile sets by layer for performance
    private readonly Dictionary<TileLayer, SemanticTile[]> _tilesByLayer;

    public SemanticWfc3dGenerator(SemanticTile[] tileSet, Vector3I mapSize, ulong seed = 0)
    {
        _tileSet = tileSet;
        _mapSize = mapSize;
        _layerOrder = new[] { TileLayer.Terrain, TileLayer.Decoration, TileLayer.Structure, TileLayer.Effects };
        _rng = new RandomNumberGenerator();
        _rng.Seed = seed;

        // Precompute tiles by layer
        _tilesByLayer = new Dictionary<TileLayer, SemanticTile[]>();
        foreach (var layer in _layerOrder)
        {
            _tilesByLayer[layer] = _tileSet.Where(t => t.Layer == layer).ToArray();
        }

        InitializeWave3D();
    }

    private void InitializeWave3D()
    {
        _wave = new List<SemanticTile>[_mapSize.Z][][];
        _collapsed = new bool[_mapSize.Z][][];

        for (int z = 0; z < _mapSize.Z; z++)
        {
            var layer = _layerOrder[z];
            var layerTiles = _tilesByLayer[layer];

            _wave[z] = new List<SemanticTile>[_mapSize.Y][];
            _collapsed[z] = new bool[_mapSize.Y][];

            for (int y = 0; y < _mapSize.Y; y++)
            {
                _wave[z][y] = new List<SemanticTile>[_mapSize.X];
                _collapsed[z][y] = new bool[_mapSize.X];

                for (int x = 0; x < _mapSize.X; x++)
                {
                    // Each position starts with all tiles valid for its layer
                    _wave[z][y][x] = new List<SemanticTile>(layerTiles);
                    _collapsed[z][y][x] = false;
                }
            }
        }
    }

    public SemanticTile[,,] Generate()
    {
        while (!IsFullyCollapsed())
        {
            var position = FindLowestEntropyCell3D();
            if (position.X == -1) break; // No valid moves

            CollapseCell3D(position);
            Propagate3D(position);
        }

        return WaveToTileArray();
    }

    private bool IsFullyCollapsed()
    {
        for (int z = 0; z < _mapSize.Z; z++)
        {
            for (int y = 0; y < _mapSize.Y; y++)
            {
                for (int x = 0; x < _mapSize.X; x++)
                {
                    if (!_collapsed[z][y][x]) return false;
                }
            }
        }

        return true;
    }

    private Vector3I FindLowestEntropyCell3D()
    {
        int minEntropy = int.MaxValue;
        var candidates = new List<Vector3I>();

        // Prioritize lower layers (terrain before structure)
        for (int z = 0; z < _mapSize.Z; z++)
        {
            for (int y = 0; y < _mapSize.Y; y++)
            {
                for (int x = 0; x < _mapSize.X; x++)
                {
                    if (_collapsed[z][y][x]) continue;

                    int entropy = _wave[z][y][x].Count;
                    if (entropy == 0) return new Vector3I(-1, -1, -1); // Impossible state

                    if (entropy < minEntropy)
                    {
                        minEntropy = entropy;
                        candidates.Clear();
                        candidates.Add(new Vector3I(x, y, z));
                    }
                    else if (entropy == minEntropy)
                    {
                        candidates.Add(new Vector3I(x, y, z));
                    }
                }
            }
        }

        if (candidates.Count == 0) return new Vector3I(-1, -1, -1);

        return candidates[_rng.RandiRange(0, candidates.Count - 1)];
    }

    private void CollapseCell3D(Vector3I position)
    {
        if (_collapsed[position.Z][position.Y][position.X] ||
            _wave[position.Z][position.Y][position.X].Count == 0) return;

        var availableTiles = _wave[position.Z][position.Y][position.X];
        var chosenTile = ChooseWeightedTile(availableTiles);

        _wave[position.Z][position.Y][position.X].Clear();
        _wave[position.Z][position.Y][position.X].Add(chosenTile);
        _collapsed[position.Z][position.Y][position.X] = true;

        // Apply any constraint modifications from the placed tile
        _constraintManager.ApplyTileConstraints(position, chosenTile);
    }

    private static Godot.Collections.Array<Core.Data.CompatibilityTag> 
        GetSocketForDirection(SemanticTile tile, Direction direction)
    {
        return direction switch
        {
            Direction.North => tile.North,
            Direction.East => tile.East,
            Direction.South => tile.South,
            Direction.West => tile.West,
            Direction.NorthEast => tile.NorthEast,
            Direction.NorthWest => tile.NorthWest,
            Direction.SouthEast => tile.SouthEast,
            Direction.SouthWest => tile.SouthWest,
            Direction.Up => tile.Up,
            Direction.Down => tile.Down,
            _ => new Godot.Collections.Array<Core.Data.CompatibilityTag>()
        };
    }


    private static Direction GetOppositeDirection(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            Direction.NorthEast => Direction.SouthWest,
            Direction.NorthWest => Direction.SouthEast,
            Direction.SouthEast => Direction.NorthWest,
            Direction.SouthWest => Direction.NorthEast,
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            _ => direction
        };
    }


    private SemanticTile ChooseWeightedTile(List<SemanticTile> tiles)
    {
        if (tiles.Count == 0) return null;
        if (tiles.Count == 1) return tiles[0];

        var totalWeight = tiles.Sum(t => t.BaseWeight);
        if (totalWeight <= 0) return tiles[0];

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

        return tiles[^1];
    }

    private SemanticTile[,,] WaveToTileArray()
    {
        var result = new SemanticTile[_mapSize.Z, _mapSize.Y, _mapSize.X];

        for (int z = 0; z < _mapSize.Z; z++)
        {
            for (int y = 0; y < _mapSize.Y; y++)
            {
                for (int x = 0; x < _mapSize.X; x++)
                {
                    if (_collapsed[z][y][x] && _wave[z][y][x].Count > 0)
                    {
                        result[z, y, x] = _wave[z][y][x][0];
                    }
                    else
                    {
                        // Fallback: get first available tile for this layer
                        var layer = _layerOrder[z];
                        var layerTiles = _tilesByLayer[layer];
                        result[z, y, x] = layerTiles.Length > 0 ? layerTiles[0] : null;
                    }
                }
            }
        }

        return result;
    }

    private Vector3I GetNeighbor3D(Vector3I position, Direction direction)
    {
        return direction switch
        {
            Direction.North => new Vector3I(position.X, position.Y - 1, position.Z),
            Direction.East => new Vector3I(position.X + 1, position.Y, position.Z),
            Direction.South => new Vector3I(position.X, position.Y + 1, position.Z),
            Direction.West => new Vector3I(position.X - 1, position.Y, position.Z),
            Direction.NorthEast => new Vector3I(position.X + 1, position.Y - 1, position.Z),
            Direction.SouthEast => new Vector3I(position.X + 1, position.Y + 1, position.Z),
            Direction.SouthWest => new Vector3I(position.X - 1, position.Y + 1, position.Z),
            Direction.NorthWest => new Vector3I(position.X - 1, position.Y - 1, position.Z),
            Direction.Up => new Vector3I(position.X, position.Y, position.Z + 1),
            Direction.Down => new Vector3I(position.X, position.Y, position.Z - 1),
            _ => position
        };
    }

    private bool IsValidCoord3D(Vector3I coord)
    {
        return coord.X >= 0 && coord.X < _mapSize.X &&
               coord.Y >= 0 && coord.Y < _mapSize.Y &&
               coord.Z >= 0 && coord.Z < _mapSize.Z;
    }

    private void Propagate3D(Vector3I position)
    {
        var stack = new Stack<Vector3I>();
        stack.Push(position);

        while (stack.Count > 0)
        {
            var pos = stack.Pop();

            // Check all 10 directions: N, S, E, W, NE, SE, SW, NW, Up, Down
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var neighbor = GetNeighbor3D(pos, direction);

                if (!IsValidCoord3D(neighbor) || _collapsed[neighbor.Z][neighbor.Y][neighbor.X])
                    continue;

                var neighborWave = _wave[neighbor.Z][neighbor.Y][neighbor.X];
                var currentWave = _wave[pos.Z][pos.Y][pos.X];
                var tilesToRemove = new List<SemanticTile>();

                foreach (var neighborTile in neighborWave)
                {
                    bool canConnect = false;

                    foreach (var currentTile in currentWave)
                    {
                        if (CanConnect3D(currentTile, neighborTile, direction, pos, neighbor))
                        {
                            canConnect = true;
                            break;
                        }
                    }

                    if (!canConnect)
                        tilesToRemove.Add(neighborTile);
                }

                if (tilesToRemove.Count > 0)
                {
                    tilesToRemove.ForEach(tile => neighborWave.Remove(tile));
                    stack.Push(neighbor);
                }
            }
        }
    }

    private bool CanConnect3D(SemanticTile from, SemanticTile to, Direction direction, Vector3I fromPos, Vector3I toPos)
    {
        // Get the target layer for the 'to' position
        var toLayer = _layerOrder[toPos.Z];

        // Check if there are active constraints for this connection
        if (toLayer == TileLayer.Terrain)
        {
            var activeConstraint = _constraintManager.GetConstraint(toPos, GetOppositeDirection(direction));
            if (activeConstraint.HasValue)
            {
                // For constraint checking, we need a simple compatibility approach
                // This is a simplified constraint check - could be enhanced based on specific needs
                var toSocket = GetSocketForDirection(to, GetOppositeDirection(direction));
                // If constraint exists, allow connection (constraint logic can be enhanced later)
                return toSocket.Count > 0;
            }
        }

        // Original connection logic for same-layer connections
        if (fromPos.Z == toPos.Z)
        {
            return from.CanConnectTo(to, direction);
        }

        // Vertical connections - use CompatibilityTag array compatibility
        if (direction == Direction.Up)
            return Core.Data.CompatibilityTag.IsArrayCompatibleWith(from.Up, to.Down);
        if (direction == Direction.Down)
            return Core.Data.CompatibilityTag.IsArrayCompatibleWith(from.Down, to.Up);

        return false;
    }
}