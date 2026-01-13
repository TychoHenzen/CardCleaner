using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Result of a unified WFC solve operation.
/// Works with both regular grids and irregular meshes.
/// </summary>
public readonly struct UnifiedWfcSolveResult
{
    public bool Success { get; }
    public int Iterations { get; }
    public string? ErrorMessage { get; }
    public int? ContradictionCellId { get; }

    private UnifiedWfcSolveResult(bool success, int iterations, string? error = null, int? contradictionCellId = null)
    {
        Success = success;
        Iterations = iterations;
        ErrorMessage = error;
        ContradictionCellId = contradictionCellId;
    }

    public static UnifiedWfcSolveResult Succeeded(int iterations) => new(true, iterations);
    public static UnifiedWfcSolveResult Failed(string error, int iterations, int? cellId = null) =>
        new(false, iterations, error, cellId);
}

/// <summary>
/// Unified WFC solver that works with any IWfcGrid implementation.
/// Supports both regular 2D grids and irregular mesh grids through the abstraction.
/// </summary>
public class UnifiedWfcSolver
{
    private readonly UnifiedWfcPropagator _propagator;
    private readonly UnifiedWfcTileSelector _selector;
    private readonly List<IUnifiedWfcConstraint> _constraints = new();

    /// <summary>
    /// Maximum iterations before giving up (prevents infinite loops).
    /// </summary>
    public int MaxIterations { get; set; } = 10000;

    /// <summary>
    /// Weight multiplier for tiles matching collapsed neighbors.
    /// </summary>
    public float ContinuityBiasMultiplier { get; set; } = 2.0f;

    public UnifiedWfcSolver(
        UnifiedWfcPropagator propagator,
        UnifiedWfcTileSelector selector)
    {
        _propagator = propagator;
        _selector = selector;
    }

    /// <summary>
    /// Adds a constraint to influence tile selection and propagation.
    /// </summary>
    public void AddConstraint(IUnifiedWfcConstraint constraint)
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
    /// Gets all registered constraints.
    /// </summary>
    public IEnumerable<IUnifiedWfcConstraint> GetConstraints() => _constraints;

    /// <summary>
    /// Runs the WFC algorithm until the grid is fully collapsed or a contradiction occurs.
    /// </summary>
    /// <param name="grid">Any IWfcGrid implementation (regular or mesh)</param>
    /// <param name="biome">Biome for soft rule weighting (optional)</param>
    /// <param name="rng">Random number generator for tile selection</param>
    /// <returns>Result indicating success or failure with details</returns>
    public UnifiedWfcSolveResult Solve(IWfcGrid grid, BiomeDefinition? biome, RandomNumberGenerator rng)
    {
        var iterations = 0;
        var totalCells = grid.CellCount;

        // Reset all constraints for fresh solve
        foreach (var constraint in _constraints)
        {
            constraint.Reset();
        }

        // Initial propagation to apply any pre-existing constraints
        var initialResult = _propagator.PropagateAll(grid);
        if (!initialResult.Success)
        {
            return UnifiedWfcSolveResult.Failed(
                "Initial propagation found contradiction",
                0,
                initialResult.ContradictionCellId);
        }

        while (!grid.IsFullyCollapsed())
        {
            iterations++;

            if (iterations > MaxIterations)
            {
                GD.Print($"[UnifiedWFC] Exceeded MaxIterations at {iterations}, collapsed ~{iterations}/{totalCells}");
                return UnifiedWfcSolveResult.Failed(
                    $"Exceeded maximum iterations ({MaxIterations})",
                    iterations);
            }

            // Progress logging every 100 iterations
            if (iterations % 100 == 0)
            {
                GD.Print($"[UnifiedWFC] Progress: {iterations}/{totalCells} cells ({100 * iterations / totalCells}%)");
            }

            // Find cell with lowest weighted entropy, preferring frontier cells
            var targetCellId = GetLowestEntropyCell(grid, biome, rng);

            if (targetCellId == null)
            {
                // All cells collapsed - we're done
                break;
            }

            var targetCell = grid.GetCell(targetCellId.Value);

            // Check for contradiction before collapse
            if (targetCell.IsContradiction())
            {
                GD.Print($"[UnifiedWFC] Contradiction at cell {targetCellId} after {iterations}/{totalCells} cells");
                return UnifiedWfcSolveResult.Failed(
                    "Found cell with no valid options",
                    iterations,
                    targetCellId);
            }

            // Get tiles matching collapsed neighbors for continuity bias
            var continuityTiles = GetContinuityMatchingTiles(grid, targetCellId.Value);

            // Select tile using weighted probabilities
            var selectedTile = _selector.SelectTile(
                targetCell.GetPossibleTiles(),
                biome,
                rng,
                continuityTiles,
                targetCellId.Value,
                grid,
                _constraints);

            if (selectedTile == null)
            {
                GD.Print($"[UnifiedWFC] Selector returned null at cell {targetCellId} after {iterations}/{totalCells} cells");
                return UnifiedWfcSolveResult.Failed(
                    "Tile selector returned null",
                    iterations,
                    targetCellId);
            }

            // Collapse the cell
            targetCell.CollapseTo(selectedTile);

            // Notify constraints of the collapse
            foreach (var constraint in _constraints)
            {
                constraint.OnTileCollapsed(targetCellId.Value, selectedTile, grid);
            }

            // Propagate constraints to neighbors
            var propResult = _propagator.Propagate(grid, targetCellId.Value);
            if (!propResult.Success)
            {
                GD.Print($"[UnifiedWFC] Propagation failed at cell {propResult.ContradictionCellId} after placing {selectedTile} at {targetCellId}");
                return UnifiedWfcSolveResult.Failed(
                    $"Propagation failed at cell {propResult.ContradictionCellId}",
                    iterations,
                    propResult.ContradictionCellId);
            }
        }

        var finalIterations = Math.Max(1, iterations);
        GD.Print($"[UnifiedWFC] Success! Completed {finalIterations} iterations for {totalCells} cells");
        return UnifiedWfcSolveResult.Succeeded(finalIterations);
    }

    /// <summary>
    /// Attempts to solve with automatic retry on contradiction.
    /// Each retry uses a fresh grid with a different random seed.
    /// </summary>
    public (UnifiedWfcSolveResult result, IWfcGrid grid) SolveWithRetry(
        Func<IWfcGrid> createGrid,
        BiomeDefinition? biome,
        ulong baseSeed,
        int maxRetries = 3)
    {
        IWfcGrid? lastGrid = null;
        UnifiedWfcSolveResult lastResult = default;

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

        return (UnifiedWfcSolveResult.Failed(
            $"All {maxRetries + 1} attempts failed. Last error: {lastResult.ErrorMessage}",
            lastResult.Iterations,
            lastResult.ContradictionCellId), lastGrid!);
    }

    /// <summary>
    /// Finds the cell with lowest weighted entropy among frontier cells.
    /// </summary>
    private int? GetLowestEntropyCell(IWfcGrid grid, BiomeDefinition? biome, RandomNumberGenerator rng)
    {
        var frontierCandidates = new List<int>();
        int? anyUncollapsed = null;
        var lowestEntropy = float.MaxValue;
        var lowestCandidates = new List<int>();

        foreach (var cellId in grid.GetAllCellIds())
        {
            var cell = grid.GetCell(cellId);
            if (cell.IsCollapsed() || cell.IsContradiction())
                continue;

            anyUncollapsed ??= cellId;

            if (grid.HasCollapsedNeighbor(cellId))
            {
                frontierCandidates.Add(cellId);
            }
        }

        // If no frontier exists (start), pick any uncollapsed
        if (frontierCandidates.Count == 0)
            return anyUncollapsed;

        // Find lowest entropy among frontier cells
        foreach (var cellId in frontierCandidates)
        {
            var cell = grid.GetCell(cellId);
            var continuityTiles = GetContinuityMatchingTiles(grid, cellId);
            var weights = _selector.ComputeWeights(
                cell.GetPossibleTiles(),
                biome,
                rng,
                continuityTiles,
                cellId,
                grid,
                _constraints);
            var entropy = cell.GetWeightedEntropy(weights);

            if (entropy < lowestEntropy)
            {
                lowestEntropy = entropy;
                lowestCandidates.Clear();
                lowestCandidates.Add(cellId);
            }
            else if (Mathf.IsEqualApprox(entropy, lowestEntropy))
            {
                lowestCandidates.Add(cellId);
            }
        }

        return lowestCandidates[rng.RandiRange(0, lowestCandidates.Count - 1)];
    }

    /// <summary>
    /// Gets the set of tile IDs from collapsed neighbors at the given cell.
    /// </summary>
    private HashSet<string>? GetContinuityMatchingTiles(IWfcGrid grid, int cellId)
    {
        HashSet<string>? result = null;

        foreach (var neighborId in grid.GetNeighbors(cellId))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighborId);
            if (neighborTile != null)
            {
                result ??= new HashSet<string>();
                result.Add(neighborTile);
            }
        }

        return result;
    }
}
