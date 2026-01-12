using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Wfc.Constraints;

/// <summary>
/// CRITICAL constraint that ensures passable vertices form a connected component.
/// Uses flood-fill to verify connectivity and prevents tiles that would create
/// isolated passable regions.
/// </summary>
public class MeshConnectivityConstraint : IMeshWfcConstraint
{
    private readonly HashSet<string> _passableTiles;
    private readonly HashSet<int> _collapsedPassableVertices = new();
    private int? _seedVertex; // First passable vertex (root of connectivity)

    /// <summary>
    /// Minimum weight returned when a tile would break connectivity.
    /// Set to 0 to completely forbid, or small positive for soft constraint.
    /// </summary>
    public float DisconnectedPenalty { get; set; } = 0.0f;

    /// <summary>
    /// Creates a connectivity constraint.
    /// </summary>
    /// <param name="passableTiles">Set of tile IDs that are considered passable.</param>
    public MeshConnectivityConstraint(IEnumerable<string> passableTiles)
    {
        _passableTiles = new HashSet<string>(passableTiles);
    }

    public float GetWeightModifier(string tileId, int vertexId, MeshWfcGrid grid)
    {
        // Non-passable tiles don't affect connectivity
        if (!_passableTiles.Contains(tileId))
            return 1.0f;

        // If no passable vertices collapsed yet, this tile is fine
        if (_collapsedPassableVertices.Count == 0)
            return 1.0f;

        // Check if this vertex would be connected to existing passable region
        if (WouldBeConnected(vertexId, grid))
            return 1.0f;

        // This tile would create an isolated passable region
        return DisconnectedPenalty;
    }

    public void OnTileCollapsed(int vertexId, string tileId, MeshWfcGrid grid)
    {
        if (!_passableTiles.Contains(tileId))
            return;

        _collapsedPassableVertices.Add(vertexId);

        // First passable vertex becomes the seed
        if (_seedVertex == null)
            _seedVertex = vertexId;
    }

    public void Reset()
    {
        _collapsedPassableVertices.Clear();
        _seedVertex = null;
    }

    /// <summary>
    /// Checks if a passable tile at the given vertex would be connected to
    /// the existing passable region.
    /// </summary>
    private bool WouldBeConnected(int vertexId, MeshWfcGrid grid)
    {
        // Check if any neighbor is already passable
        foreach (var neighborId in grid.GetNeighbors(vertexId))
        {
            if (_collapsedPassableVertices.Contains(neighborId))
                return true;
        }

        // No direct connection to existing passable region
        // However, if this is close enough to potentially connect later,
        // we can be more lenient
        return false;
    }

    /// <summary>
    /// Validates that all passable vertices form a single connected component.
    /// Call this after WFC completes to verify the result.
    /// </summary>
    public bool ValidateConnectivity(MeshWfcGrid grid)
    {
        if (_collapsedPassableVertices.Count <= 1)
            return true;

        if (_seedVertex == null)
            return true;

        // Flood fill from seed vertex
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(_seedVertex.Value);
        visited.Add(_seedVertex.Value);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighborId in grid.GetNeighbors(current))
            {
                if (visited.Contains(neighborId))
                    continue;

                if (!_collapsedPassableVertices.Contains(neighborId))
                    continue;

                visited.Add(neighborId);
                queue.Enqueue(neighborId);
            }
        }

        // All passable vertices should be reachable from seed
        return visited.Count == _collapsedPassableVertices.Count;
    }

    /// <summary>
    /// Gets the number of disconnected passable regions (for debugging).
    /// Returns 0 if fully connected, >0 if there are isolated regions.
    /// </summary>
    public int CountDisconnectedRegions(MeshWfcGrid grid)
    {
        if (_collapsedPassableVertices.Count == 0)
            return 0;

        var visited = new HashSet<int>();
        var regionCount = 0;

        foreach (var startVertex in _collapsedPassableVertices)
        {
            if (visited.Contains(startVertex))
                continue;

            // New region found
            regionCount++;

            // Flood fill this region
            var queue = new Queue<int>();
            queue.Enqueue(startVertex);
            visited.Add(startVertex);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var neighborId in grid.GetNeighbors(current))
                {
                    if (visited.Contains(neighborId))
                        continue;

                    if (!_collapsedPassableVertices.Contains(neighborId))
                        continue;

                    visited.Add(neighborId);
                    queue.Enqueue(neighborId);
                }
            }
        }

        // Return number of extra regions (0 = connected, 1+ = disconnected)
        return regionCount - 1;
    }
}
