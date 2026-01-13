using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Defines which terrain tiles can be adjacent to each other based on
/// the compiled transition map. Used by WFC to enforce hard constraints.
/// </summary>
public class WfcAdjacencyRules
{
    private readonly Dictionary<string, HashSet<string>> _adjacencyMap = new();
    private readonly HashSet<string> _allTileIds = new();

    /// <summary>
    /// Gets all tile IDs that have defined adjacency rules.
    /// </summary>
    public IReadOnlySet<string> AllTileIds => _allTileIds;

    /// <summary>
    /// Creates adjacency rules from a compiled transition resolver.
    /// Pre-computes bidirectional adjacency for O(1) queries.
    /// </summary>
    public WfcAdjacencyRules(CompiledTransitionResolver resolver)
    {
        BuildAdjacencyMap(resolver);
    }

    /// <summary>
    /// Creates adjacency rules from explicit transition pairs.
    /// Useful for testing or custom rule sets.
    /// </summary>
    public WfcAdjacencyRules(IEnumerable<(string tileA, string tileB)> transitionPairs)
    {
        foreach (var (tileA, tileB) in transitionPairs)
        {
            AddTransition(tileA, tileB);
        }

        // Ensure all tiles can be adjacent to themselves
        foreach (var tileId in _allTileIds)
        {
            _adjacencyMap[tileId].Add(tileId);
        }
    }

    /// <summary>
    /// Checks if two tiles can be placed adjacent to each other.
    /// Tiles can always be adjacent to themselves (same terrain needs no transition).
    /// </summary>
    public bool CanBeAdjacent(string tileA, string tileB)
    {
        if (tileA == tileB)
            return true;

        return _adjacencyMap.TryGetValue(tileA, out var neighbors) && neighbors.Contains(tileB);
    }

    /// <summary>
    /// Gets all tiles that can be adjacent to the given tile.
    /// Always includes the tile itself.
    /// </summary>
    public IReadOnlySet<string> GetValidNeighbors(string tileId)
    {
        if (_adjacencyMap.TryGetValue(tileId, out var neighbors))
            return neighbors;

        // Unknown tile - can only be adjacent to itself
        return new HashSet<string> { tileId };
    }

    /// <summary>
    /// Gets tiles that are valid neighbors for ALL of the given tiles.
    /// Used to compute valid options for a cell given its constrained neighbors.
    /// </summary>
    public HashSet<string> GetCommonValidNeighbors(IEnumerable<string> tiles)
    {
        HashSet<string>? result = null;

        foreach (var tile in tiles)
        {
            var neighbors = GetValidNeighbors(tile);
            if (result == null)
            {
                result = new HashSet<string>(neighbors);
            }
            else
            {
                result.IntersectWith(neighbors);
            }
        }

        return result ?? new HashSet<string>();
    }

    private void BuildAdjacencyMap(CompiledTransitionResolver resolver)
    {
        // Extract all transition pairs from the resolver
        foreach (var (innerTerrain, outerTerrain) in resolver.GetAllTransitionPairs())
        {
            AddTransition(innerTerrain, outerTerrain);
        }

        // Ensure all tiles can be adjacent to themselves
        foreach (var tileId in _allTileIds)
        {
            _adjacencyMap[tileId].Add(tileId);
        }
    }

    private void AddTransition(string tileA, string tileB)
    {
        // Track all tile IDs
        _allTileIds.Add(tileA);
        _allTileIds.Add(tileB);

        // Adjacency is symmetric: if A can transition to B, they can be neighbors
        GetOrCreateNeighborSet(tileA).Add(tileB);
        GetOrCreateNeighborSet(tileB).Add(tileA);
    }

    private HashSet<string> GetOrCreateNeighborSet(string tileId)
    {
        if (!_adjacencyMap.TryGetValue(tileId, out var neighbors))
        {
            neighbors = new HashSet<string>();
            _adjacencyMap[tileId] = neighbors;
        }
        return neighbors;
    }

    /// <summary>
    /// Makes a group of tiles mutually adjacent to each other.
    /// Used for gap tiles (non-auto-tiles) which should be able to border any other gap tile
    /// in the background layer of two-phase WFC.
    /// </summary>
    public void AddMutualAdjacencies(IEnumerable<string> tileIds)
    {
        var tiles = new List<string>(tileIds);
        foreach (var tileA in tiles)
        {
            _allTileIds.Add(tileA);
            var neighborsA = GetOrCreateNeighborSet(tileA);
            foreach (var tileB in tiles)
            {
                neighborsA.Add(tileB);
            }
        }
    }

    /// <summary>
    /// Adds bidirectional adjacency between two tiles.
    /// </summary>
    public void AddAdjacency(string tileA, string tileB)
    {
        _allTileIds.Add(tileA);
        _allTileIds.Add(tileB);

        GetOrCreateNeighborSet(tileA).Add(tileB);
        GetOrCreateNeighborSet(tileB).Add(tileA);
    }

    /// <summary>
    /// Ensures a tile can be adjacent to itself (required for WFC).
    /// Adds tile to the rules if not already present.
    /// </summary>
    public void EnsureSelfAdjacency(string tileId)
    {
        _allTileIds.Add(tileId);
        GetOrCreateNeighborSet(tileId).Add(tileId);
    }
}
