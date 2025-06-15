using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;


namespace CardCleaner.Scripts.Features.Worldgen;

public class SemanticWfc3dGenerator
{
    private readonly SemanticTile[] _tileSet;
    private readonly Vector3I _mapSize; // X, Y, Z (where Z = number of layers)
    private readonly TileLayer[] _layerOrder; // [Terrain, Decoration, Structure, Effects]
    private readonly RandomNumberGenerator _rng;
    private readonly ConstraintManager _constraintManager = new();
    private readonly GradientInfluenceComponent _gradientInfluence;


    // 3D wave function: [layer][y][x]
    private List<SemanticTile>[][][] _wave;
    private bool[][][] _collapsed;

    // Precomputed tile sets by layer for performance
    private readonly Dictionary<TileLayer, SemanticTile[]> _tilesByLayer;

    public SemanticWfc3dGenerator(SemanticTile[] tileSet, Vector3I mapSize, RandomNumberGenerator rng,
        GradientInfluenceComponent? gradientInfluence = null)
    {
        _rng = rng;
        _tileSet = tileSet;
        _mapSize = mapSize;
        _gradientInfluence = gradientInfluence ?? new GradientInfluenceComponent(new RadialGradient());

        _layerOrder = new[] { TileLayer.Terrain, TileLayer.Decoration, TileLayer.Structure, TileLayer.Effects };

        // Precompute tiles by layer
        _tilesByLayer = new Dictionary<TileLayer, SemanticTile[]>();
        foreach (var layer in _layerOrder)
        {
            _tilesByLayer[layer] = _tileSet.Where(t => t.Layer == layer).ToArray();
        }

        foreach (var kvp in _tilesByLayer)
        {
            ILog.Print($"Layer {kvp.Key}: {kvp.Value.Length} tiles");
            foreach (var tile in kvp.Value)
            {
                ILog.Print($"  - {tile.TileName}");
            }
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

            _wave[z] = new List<SemanticTile>[_mapSize.Y][];
            _collapsed[z] = new bool[_mapSize.Y][];

            for (int y = 0; y < _mapSize.Y; y++)
            {
                _wave[z][y] = new List<SemanticTile>[_mapSize.X];
                _collapsed[z][y] = new bool[_mapSize.X];

                for (int x = 0; x < _mapSize.X; x++)
                {
                    _collapsed[z][y][x] = false;

                    if (layer == TileLayer.Terrain)
                    {
                        var layerTiles = _tilesByLayer[layer];
                        _wave[z][y][x] = new List<SemanticTile>(layerTiles);

                        // Debug: Check if any constraints immediately eliminate tiles
                        if (x == 0 || y == 0 || x == _mapSize.X - 1 || y == _mapSize.Y - 1)
                        {
                            ILog.Print($"Boundary cell [{x},{y}]: {_wave[z][y][x].Count} tiles initially");
                        }
                    }

                    else
                    {
                        // Upper layers: start with all tiles, but spawning will be controlled during collapse
                        var layerTiles = _tilesByLayer[layer];
                        _wave[z][y][x] = new List<SemanticTile>(layerTiles);
                    }
                }
            }
        }

        ILog.Print("=== TESTING INITIAL PROPAGATION ===");
        TestInitialConstraints();
        TestSocketCompatibility();
    }

    private void TestSocketCompatibility()
    {
        ILog.Print("=== SOCKET COMPATIBILITY TEST ===");

        var tiles = _tilesByLayer[TileLayer.Terrain];

        foreach (var tileA in tiles)
        {
            foreach (var tileB in tiles)
            {
                // Test North-South connection
                var canConnect = CanConnect3D(tileA, tileB, Direction.North, Vector3I.Zero, new Vector3I(0, -1, 0));
                ILog.Print($"{tileA.TileName} → {tileB.TileName} (North): {canConnect}");

                if (!canConnect)
                {
                    // Debug why it failed
                    var fromSocket = GetSocketForDirection(tileA, Direction.North);
                    var toSocket = GetSocketForDirection(tileB, Direction.South);

                    ILog.Print($"  From socket: [{string.Join(", ", fromSocket.Select(t => t.Tag))}]");
                    ILog.Print($"  To socket: [{string.Join(", ", toSocket.Select(t => t.Tag))}]");

                    // Test individual tag compatibility
                    foreach (var fromTag in fromSocket)
                    {
                        foreach (var toTag in toSocket)
                        {
                            var compatible = fromTag.IsCompatibleWith(toTag);
                            ILog.Print($"    {fromTag.Tag} ↔ {toTag.Tag}: {compatible}");
                        }
                    }
                }
            }
        }
    }

    private void TestInitialConstraints()
    {
        for (int y = 0; y < _mapSize.Y; y++)
        {
            for (int x = 0; x < _mapSize.X; x++)
            {
                var position = new Vector3I(x, y, 0); // Terrain layer
                var availableTiles = _wave[0][y][x].ToList();

                foreach (var tile in availableTiles.ToList())
                {
                    bool canPlace = true;
                    string failReason = "";

                    // Check each direction for constraint violations
                    foreach (var direction in new[]
                                 { Direction.North, Direction.East, Direction.South, Direction.West })
                    {
                        var neighbor = GetNeighbor3D(position, direction);

                        if (!IsValidCoord3D(neighbor))
                        {
                            // Boundary - what constraints exist here?
                            continue;
                        }

                        var neighborTiles = _wave[neighbor.Z][neighbor.Y][neighbor.X];
                        bool hasValidConnection = false;

                        foreach (var neighborTile in neighborTiles)
                        {
                            if (CanConnect3D(tile, neighborTile, direction, position, neighbor))
                            {
                                hasValidConnection = true;
                                break;
                            }
                        }

                        if (!hasValidConnection)
                        {
                            canPlace = false;
                            failReason = $"No valid connection in direction {direction}";
                            break;
                        }
                    }

                    if (!canPlace)
                    {
                        ILog.Print($"Tile {tile.TileName} eliminated from [{x},{y}]: {failReason}");
                        _wave[0][y][x].Remove(tile);
                    }
                }

                if (_wave[0][y][x].Count == 0)
                {
                    ILog.Print($"IMPOSSIBLE STATE at [{x},{y}] - no valid tiles remain!");
                }
            }
        }
    }

    public SemanticTile[,,] Generate()
    {
        ILog.Print("=== WFC GENERATION START ===");

        int iterations = 0;
        while (!IsFullyCollapsed())
        {
            iterations++;
            ILog.Print($"Iteration {iterations}");

            var position = FindLowestEntropyCell3D();
            ILog.Print($"Lowest entropy position: {position}");

            if (position.X == -1)
            {
                ILog.Print("ERROR: No valid moves found - impossible state!");
                break;
            }

            ILog.Print($"Entropy at {position}: {_wave[position.Z][position.Y][position.X].Count}");

            CollapseCell3D(position);
            Propagate3D(position);

            if (iterations > 100) // Safety break
            {
                ILog.Print("ERROR: Too many iterations!");
                break;
            }
        }

        ILog.Print($"=== WFC GENERATION COMPLETE: {iterations} iterations ===");
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
        int totalCells = 0;
        int collapsedCells = 0;
        int zeroCells = 0;
        var entropyCounts = new Dictionary<int, int>();

        // Prioritize lower layers (terrain before structure)
        for (int z = 0; z < _mapSize.Z; z++)
        {
            for (int y = 0; y < _mapSize.Y; y++)
            {
                for (int x = 0; x < _mapSize.X; x++)
                {
                    totalCells++;

                    if (_collapsed[z][y][x])
                    {
                        collapsedCells++;
                        continue;
                    }

                    int entropy = _wave[z][y][x].Count;

                    // Track entropy distribution
                    if (!entropyCounts.ContainsKey(entropy))
                        entropyCounts[entropy] = 0;
                    entropyCounts[entropy]++;

                    if (entropy == 0)
                    {
                        zeroCells++;
                        ILog.Print($"ZERO ENTROPY at [{x},{y},{z}]!");
                        continue;
                    }

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

        ILog.Print($"=== ENTROPY ANALYSIS ===");
        ILog.Print($"Total cells: {totalCells}, Collapsed: {collapsedCells}, Zero entropy: {zeroCells}");
        ILog.Print($"Min entropy: {minEntropy}, Candidates: {candidates.Count}");

        foreach (var kvp in entropyCounts.OrderBy(x => x.Key))
        {
            ILog.Print($"  Entropy {kvp.Key}: {kvp.Value} cells");
        }

        if (candidates.Count == 0) return new Vector3I(-1, -1, -1);

        return candidates[_rng.RandiRange(0, candidates.Count - 1)];
    }

    private void CollapseCell3D(Vector3I position)
    {
        if (_collapsed[position.Z][position.Y][position.X] ||
            _wave[position.Z][position.Y][position.X].Count == 0) return;

        var currentLayer = _layerOrder[position.Z];

        // For upper layers, check if anything should spawn based on layers below
        if (currentLayer != TileLayer.Terrain)
        {
            bool shouldSpawn = ShouldSpawnBasedOnLayersBelow(position);

            if (!shouldSpawn)
            {
                // Force empty cell
                _wave[position.Z][position.Y][position.X].Clear();
                _collapsed[position.Z][position.Y][position.X] = true;
                return;
            }
        }

        var availableTiles = _wave[position.Z][position.Y][position.X];

        if (availableTiles.Count == 0) return;

        var chosenTile = ChooseWeightedTile(availableTiles, position);

        _wave[position.Z][position.Y][position.X].Clear();
        _wave[position.Z][position.Y][position.X].Add(chosenTile);
        _collapsed[position.Z][position.Y][position.X] = true;

        _constraintManager.ApplyTileConstraints(position, chosenTile);
    }

    private static Godot.Collections.Array<CompatibilityTag>
        GetSocketForDirection(SemanticTile tile, Direction direction)
    {
        return direction switch
        {
            Direction.North => tile.SocketData.North,
            Direction.East => tile.SocketData.East,
            Direction.South => tile.SocketData.South,
            Direction.West => tile.SocketData.West,
            Direction.NorthEast => tile.SocketData.NorthEast,
            Direction.NorthWest => tile.SocketData.NorthWest,
            Direction.SouthEast => tile.SocketData.SouthEast,
            Direction.SouthWest => tile.SocketData.SouthWest,
            Direction.Up => tile.SocketData.Up,
            Direction.Down => tile.SocketData.Down,
            _ => new Godot.Collections.Array<CompatibilityTag>()
        };
    }

    private bool ShouldSpawnBasedOnLayersBelow(Vector3I position)
    {
        // Check terrain layer (layer 0) for GlobalSpawnChance
        if (_collapsed[0][position.Y][position.X] && _wave[0][position.Y][position.X].Count > 0)
        {
            var terrainTile = _wave[0][position.Y][position.X][0];
            if (terrainTile != null)
            {
                // Roll against the terrain tile's GlobalSpawnChance
                bool baseSpawnRoll = _rng.Randf() < terrainTile.GlobalSpawnChance;

                // Also check for connectivity from neighbors (for border tiles)
                bool shouldPropagateFromNeighbors = CheckConnectivityPropagation(position);

                return baseSpawnRoll || shouldPropagateFromNeighbors;
            }
        }

        // If terrain isn't collapsed yet, allow spawning for now (WFC will sort it out)
        // This prevents deadlocks during generation
        return true;
    }

    private bool CheckConnectivityPropagation(Vector3I position)
    {
        var directions = new[]
        {
            Direction.North, Direction.East, Direction.South, Direction.West
        };

        foreach (var direction in directions)
        {
            var neighbor = GetNeighbor3D(position, direction);
            if (!IsValidCoord3D(neighbor)) continue;

            // Only check already collapsed neighbors to avoid circular dependencies
            if (!_collapsed[neighbor.Z][neighbor.Y][neighbor.X] ||
                _wave[neighbor.Z][neighbor.Y][neighbor.X].Count <= 0) continue;
            var neighborTile = _wave[neighbor.Z][neighbor.Y][neighbor.X][0];
            if (neighborTile is { HasLayerConstraints: true })
                return true;
        }

        return false;
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

    private SemanticTile ChooseWeightedTile(List<SemanticTile> tiles, Vector3I position)
    {
        if (tiles.Count == 0) return null;
        if (tiles.Count == 1) return tiles[0];

        var weights = tiles.Select(t => t.BaseWeight).ToList();
        var totalWeight = weights.Sum();

        if (totalWeight <= 0) return tiles[0];

        var randomValue = _rng.Randf() * totalWeight;

        // DEBUG: Log the selection process
        ILog.Print($"=== TILE SELECTION DEBUG ===");
        ILog.Print($"Position: {position}");
        ILog.Print($"Total weight: {totalWeight}");
        ILog.Print($"Random value: {randomValue}");

        var currentWeight = 0f;
        for (int i = 0; i < tiles.Count; i++)
        {
            currentWeight += weights[i];
            ILog.Print($"  Tile {i}: {tiles[i].TileName}, weight: {weights[i]}, cumulative: {currentWeight}");

            if (randomValue <= currentWeight)
            {
                ILog.Print($"  >>> SELECTED: {tiles[i].TileName} <<<");
                return tiles[i];
            }
        }

        ILog.Print($"  >>> FALLBACK: {tiles[^1].TileName} <<<");
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
        var activeConstraints = _constraintManager.GetConstraints(toPos, toLayer);

        // Get the target socket direction (opposite of connection direction)
        var targetSocketDirection = GetOppositeDirection(direction);
        var toSocket = GetSocketForDirection(to, targetSocketDirection);

        // Apply constraints to modify the target socket if any exist
        var layerConstraints = activeConstraints as LayerConstraint[] ?? activeConstraints.ToArray();
        if (layerConstraints.Length != 0)
        {
            toSocket = ApplyConstraintsToSocket(toSocket, layerConstraints, targetSocketDirection);
        }

        // Original connection logic for same-layer connections
        if (fromPos.Z == toPos.Z)
        {
            var fromSocket = GetSocketForDirection(from, direction);
            return fromSocket.Any(fromTag => toSocket.Any(fromTag.IsCompatibleWith));
        }

        // Vertical connections - use CompatibilityTag array compatibility
        if (direction == Direction.Up)
            return CompatibilityTag.IsArrayCompatibleWith(from.SocketData.Up, toSocket);
        if (direction == Direction.Down)
            return CompatibilityTag.IsArrayCompatibleWith(from.SocketData.Down, toSocket);

        return false;
    }

    private Godot.Collections.Array<CompatibilityTag> ApplyConstraintsToSocket(
        Godot.Collections.Array<CompatibilityTag> originalSocket,
        IEnumerable<LayerConstraint> constraints,
        Direction socketDirection)
    {
        // Create a mutable copy of the original socket
        var modifiedSocket = new Godot.Collections.Array<CompatibilityTag>(originalSocket);

        // Apply each constraint that affects this socket direction
        foreach (var constraint in constraints.Where(c => c.AffectedSocket == socketDirection))
        {
            switch (constraint.operation)
            {
                case LayerConstraint.Operation.Add:
                    if (!modifiedSocket.Contains(constraint.tag))
                    {
                        modifiedSocket.Add(constraint.tag);
                    }

                    break;
                case LayerConstraint.Operation.Remove:
                    modifiedSocket.Remove(constraint.tag);
                    break;
            }
        }

        return modifiedSocket;
    }
}