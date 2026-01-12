using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Wfc.Constraints;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Result of a mesh WFC solve attempt.
/// </summary>
public readonly struct MeshWfcSolveResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public int Iterations { get; }
    public int? ContradictionVertex { get; }

    private MeshWfcSolveResult(bool success, string? error, int iterations, int? contradiction)
    {
        Success = success;
        ErrorMessage = error;
        Iterations = iterations;
        ContradictionVertex = contradiction;
    }

    public static MeshWfcSolveResult Succeeded(int iterations) =>
        new(true, null, iterations, null);

    public static MeshWfcSolveResult Failed(string error, int iterations, int? contradiction = null) =>
        new(false, error, iterations, contradiction);
}

/// <summary>
/// WFC solver for irregular mesh terrain generation.
/// Operates on mesh vertices instead of 2D grid cells.
/// </summary>
public class MeshWfcSolver
{
    private readonly Dictionary<string, HashSet<string>> _adjacencyRules;
    private readonly Dictionary<string, float> _tileWeights;
    private readonly List<IMeshWfcConstraint> _constraints = new();

    /// <summary>
    /// Maximum iterations before giving up.
    /// </summary>
    public int MaxIterations { get; set; } = 10000;

    /// <summary>
    /// Adds a constraint to influence tile selection.
    /// </summary>
    public void AddConstraint(IMeshWfcConstraint constraint)
    {
        _constraints.Add(constraint);
    }

    /// <summary>
    /// Removes all constraints.
    /// </summary>
    public void ClearConstraints()
    {
        _constraints.Clear();
    }

    /// <summary>
    /// Creates a mesh WFC solver.
    /// </summary>
    /// <param name="adjacencyRules">Mapping of tile ID to valid adjacent tile IDs.</param>
    /// <param name="tileWeights">Base probability weights for each tile.</param>
    public MeshWfcSolver(
        Dictionary<string, HashSet<string>> adjacencyRules,
        Dictionary<string, float>? tileWeights = null)
    {
        _adjacencyRules = adjacencyRules;
        _tileWeights = tileWeights ?? new Dictionary<string, float>();
    }

    /// <summary>
    /// Creates a solver from a tile registry that supports all tiles as adjacent.
    /// </summary>
    public static MeshWfcSolver CreatePermissive(IEnumerable<string> tileIds)
    {
        var allTiles = new HashSet<string>(tileIds);
        var rules = allTiles.ToDictionary(t => t, _ => new HashSet<string>(allTiles));
        return new MeshWfcSolver(rules);
    }

    /// <summary>
    /// Runs WFC on the mesh grid.
    /// </summary>
    public MeshWfcSolveResult Solve(MeshWfcGrid grid, BiomeDefinition? biome, RandomNumberGenerator rng)
    {
        var iterations = 0;

        // Reset constraints for new solve
        foreach (var constraint in _constraints)
        {
            constraint.Reset();
        }

        // Initial propagation
        var initialResult = PropagateAll(grid);
        if (!initialResult.success)
        {
            return MeshWfcSolveResult.Failed(
                "Initial propagation found contradiction",
                0,
                initialResult.contradiction);
        }

        while (!grid.IsFullyCollapsed())
        {
            iterations++;

            if (iterations > MaxIterations)
            {
                GD.Print($"[MeshWFC] Exceeded MaxIterations at {iterations}");
                return MeshWfcSolveResult.Failed(
                    $"Exceeded maximum iterations ({MaxIterations})",
                    iterations);
            }

            if (iterations % 100 == 0)
            {
                GD.Print($"[MeshWFC] Progress: {iterations}/{grid.CellCount} cells");
            }

            // Find cell with lowest entropy (prefer frontier cells)
            var targetVertex = GetLowestEntropyVertex(grid, biome, rng);
            if (targetVertex == null)
            {
                break; // All collapsed
            }

            var targetCell = grid.GetCell(targetVertex.Value);

            if (targetCell.IsContradiction())
            {
                return MeshWfcSolveResult.Failed(
                    "Found vertex with no valid options",
                    iterations,
                    targetVertex);
            }

            // Select tile using weighted probabilities
            var selectedTile = SelectTile(targetCell, grid, targetVertex.Value, biome, rng);
            if (selectedTile == null)
            {
                return MeshWfcSolveResult.Failed(
                    "Tile selector returned null",
                    iterations,
                    targetVertex);
            }

            // Collapse the cell
            targetCell.CollapseTo(selectedTile);

            // Notify constraints of the collapse
            foreach (var constraint in _constraints)
            {
                constraint.OnTileCollapsed(targetVertex.Value, selectedTile, grid);
            }

            // Propagate constraints to neighbors
            var propResult = Propagate(grid, targetVertex.Value);
            if (!propResult.success)
            {
                return MeshWfcSolveResult.Failed(
                    $"Propagation failed at vertex {propResult.contradiction}",
                    iterations,
                    propResult.contradiction);
            }
        }

        // Ensure at least 1 iteration is reported for a successful solve
        // (counts initial propagation even if all cells were pre-collapsed)
        var finalIterations = Math.Max(1, iterations);
        GD.Print($"[MeshWFC] Success! Completed {finalIterations} iterations for {grid.CellCount} vertices");
        return MeshWfcSolveResult.Succeeded(finalIterations);
    }

    /// <summary>
    /// Solves with automatic retry on contradiction.
    /// </summary>
    public (MeshWfcSolveResult result, MeshWfcGrid grid) SolveWithRetry(
        Func<MeshWfcGrid> createGrid,
        BiomeDefinition? biome,
        ulong baseSeed,
        int maxRetries = 3)
    {
        MeshWfcGrid? lastGrid = null;
        MeshWfcSolveResult lastResult = default;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var grid = createGrid();
            lastGrid = grid;

            var rng = new RandomNumberGenerator();
            rng.Seed = baseSeed + (ulong)attempt;

            lastResult = Solve(grid, biome, rng);

            if (lastResult.Success)
            {
                return (lastResult, grid);
            }
        }

        return (MeshWfcSolveResult.Failed(
            $"All {maxRetries + 1} attempts failed. Last error: {lastResult.ErrorMessage}",
            lastResult.Iterations,
            lastResult.ContradictionVertex), lastGrid!);
    }

    private int? GetLowestEntropyVertex(MeshWfcGrid grid, BiomeDefinition? biome, RandomNumberGenerator rng)
    {
        var frontierCandidates = new List<int>();
        int? anyUncollapsed = null;
        var lowestEntropy = float.MaxValue;
        var lowestCandidates = new List<int>();

        foreach (var vertexId in grid.GetAllVertexIds())
        {
            var cell = grid.GetCell(vertexId);
            if (cell.IsCollapsed() || cell.IsContradiction())
                continue;

            anyUncollapsed ??= vertexId;

            if (grid.HasCollapsedNeighbor(vertexId))
            {
                frontierCandidates.Add(vertexId);
            }
        }

        // If no frontier exists (start), pick any uncollapsed
        if (frontierCandidates.Count == 0)
            return anyUncollapsed;

        // Find lowest entropy among frontier cells
        foreach (var vertexId in frontierCandidates)
        {
            var cell = grid.GetCell(vertexId);
            var weights = GetTileWeights(cell.GetPossibleTiles(), biome);
            var entropy = cell.GetWeightedEntropy(weights);

            if (entropy < lowestEntropy)
            {
                lowestEntropy = entropy;
                lowestCandidates.Clear();
                lowestCandidates.Add(vertexId);
            }
            else if (Mathf.IsEqualApprox(entropy, lowestEntropy))
            {
                lowestCandidates.Add(vertexId);
            }
        }

        return lowestCandidates[rng.RandiRange(0, lowestCandidates.Count - 1)];
    }

    private string? SelectTile(
        Wfc.WfcCellState cell,
        MeshWfcGrid grid,
        int vertexId,
        BiomeDefinition? biome,
        RandomNumberGenerator rng)
    {
        var possibleTiles = cell.GetPossibleTiles();
        if (possibleTiles.Count == 0)
            return null;

        // Apply continuity bias - prefer tiles that match neighbors
        var continuityTiles = new HashSet<string>();
        foreach (var neighborId in grid.GetNeighbors(vertexId))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighborId);
            if (neighborTile != null)
                continuityTiles.Add(neighborTile);
        }

        // Calculate weights
        var weights = GetTileWeights(possibleTiles, biome);

        // Apply continuity boost
        if (continuityTiles.Count > 0)
        {
            foreach (var tile in continuityTiles)
            {
                if (weights.ContainsKey(tile))
                {
                    weights[tile] *= 2.0f; // Boost matching tiles
                }
            }
        }

        // Apply constraint modifiers
        foreach (var tile in weights.Keys.ToList())
        {
            foreach (var constraint in _constraints)
            {
                var modifier = constraint.GetWeightModifier(tile, vertexId, grid);
                weights[tile] *= modifier;
            }
        }

        // Select based on weights
        var totalWeight = weights.Values.Sum();
        if (totalWeight <= 0)
            return possibleTiles.First();

        var roll = rng.Randf() * totalWeight;
        var cumulative = 0f;

        foreach (var (tile, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return tile;
        }

        return possibleTiles.Last();
    }

    private Dictionary<string, float> GetTileWeights(IEnumerable<string> tiles, BiomeDefinition? biome)
    {
        var weights = new Dictionary<string, float>();

        foreach (var tile in tiles)
        {
            var baseWeight = _tileWeights.TryGetValue(tile, out var w) ? w : 1.0f;

            // Apply biome preference if available
            if (biome != null)
            {
                // Boost passable tiles, reduce blocked tiles
                if (biome.PassableTiles.GetAllTileIds().Contains(tile))
                    baseWeight *= 1.5f;
                else if (biome.BlockedTiles.GetAllTileIds().Contains(tile))
                    baseWeight *= 0.5f;
            }

            weights[tile] = baseWeight;
        }

        return weights;
    }

    private (bool success, int? contradiction) PropagateAll(MeshWfcGrid grid)
    {
        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var vertexId in grid.GetAllVertexIds())
            {
                var cell = grid.GetCell(vertexId);
                if (cell.IsCollapsed())
                    continue;

                var validTiles = GetValidTilesForVertex(grid, vertexId);
                var removed = cell.IntersectWith(validTiles);

                if (removed)
                {
                    changed = true;
                    if (cell.IsContradiction())
                        return (false, vertexId);
                }
            }
        }

        return (true, null);
    }

    private (bool success, int? contradiction) Propagate(MeshWfcGrid grid, int fromVertex)
    {
        var queue = new Queue<int>();
        var visited = new HashSet<int>();

        // Start with neighbors of collapsed cell
        foreach (var neighborId in grid.GetNeighbors(fromVertex))
        {
            queue.Enqueue(neighborId);
        }

        while (queue.Count > 0)
        {
            var vertexId = queue.Dequeue();
            if (visited.Contains(vertexId))
                continue;

            visited.Add(vertexId);

            var cell = grid.GetCell(vertexId);
            if (cell.IsCollapsed())
                continue;

            var validTiles = GetValidTilesForVertex(grid, vertexId);
            var removed = cell.IntersectWith(validTiles);

            if (removed)
            {
                if (cell.IsContradiction())
                    return (false, vertexId);

                // Add neighbors to queue
                foreach (var neighborId in grid.GetNeighbors(vertexId))
                {
                    if (!visited.Contains(neighborId))
                        queue.Enqueue(neighborId);
                }
            }
        }

        return (true, null);
    }

    private HashSet<string> GetValidTilesForVertex(MeshWfcGrid grid, int vertexId)
    {
        var cell = grid.GetCell(vertexId);
        var possibleTiles = cell.GetPossibleTiles();

        // If no collapsed neighbors, all tiles are valid
        var collapsedNeighbors = new List<string>();
        foreach (var neighborId in grid.GetNeighbors(vertexId))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighborId);
            if (neighborTile != null)
                collapsedNeighbors.Add(neighborTile);
        }

        if (collapsedNeighbors.Count == 0)
            return new HashSet<string>(possibleTiles);

        // Filter to tiles that can be adjacent to all collapsed neighbors
        var valid = new HashSet<string>();
        foreach (var tile in possibleTiles)
        {
            if (!_adjacencyRules.TryGetValue(tile, out var allowedNeighbors))
            {
                // No rules for this tile - assume all neighbors valid
                valid.Add(tile);
                continue;
            }

            var allNeighborsValid = collapsedNeighbors.All(n =>
                allowedNeighbors.Contains(n));

            if (allNeighborsValid)
                valid.Add(tile);
        }

        return valid;
    }
}
