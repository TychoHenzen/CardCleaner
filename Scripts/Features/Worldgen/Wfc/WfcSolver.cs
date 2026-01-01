using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Result of a WFC solve operation.
/// </summary>
public readonly struct WfcSolveResult
{
    public bool Success { get; }
    public int Iterations { get; }
    public string? ErrorMessage { get; }
    public Vector2I? ContradictionPosition { get; }

    private WfcSolveResult(bool success, int iterations, string? error = null, Vector2I? contradictionPos = null)
    {
        Success = success;
        Iterations = iterations;
        ErrorMessage = error;
        ContradictionPosition = contradictionPos;
    }

    public static WfcSolveResult Succeeded(int iterations) => new(true, iterations);
    public static WfcSolveResult Failed(string error, int iterations, Vector2I? pos = null) =>
        new(false, iterations, error, pos);
}

/// <summary>
/// Main WFC algorithm solver. Orchestrates cell selection, collapse, and propagation.
/// </summary>
public class WfcSolver
{
    private readonly WfcPropagator _propagator;
    private readonly WfcTileSelector _selector;
    private readonly BlobSizeTracker? _blobTracker;

    /// <summary>
    /// Maximum iterations before giving up (prevents infinite loops).
    /// Default is 10000 which should handle up to ~100x100 grids.
    /// </summary>
    public int MaxIterations { get; set; } = 10000;

    public WfcSolver(WfcPropagator propagator, WfcTileSelector selector, BlobSizeTracker? blobTracker = null)
    {
        _propagator = propagator;
        _selector = selector;
        _blobTracker = blobTracker;
    }

    /// <summary>
    /// Runs the WFC algorithm until the grid is fully collapsed or a contradiction occurs.
    /// </summary>
    /// <param name="grid">The WFC grid to solve</param>
    /// <param name="biome">Biome for soft rule weighting</param>
    /// <param name="rng">Random number generator for tile selection</param>
    /// <returns>Result indicating success or failure with details</returns>
    public WfcSolveResult Solve(WfcGrid grid, BiomeDefinition? biome, RandomNumberGenerator rng)
    {
        var iterations = 0;

        // Clear blob tracker for fresh solve
        _blobTracker?.Clear();

        // Initial propagation to apply any pre-existing constraints
        var initialResult = _propagator.PropagateAll(grid);
        if (!initialResult.Success)
        {
            return WfcSolveResult.Failed(
                "Initial propagation found contradiction",
                0,
                initialResult.ContradictionPosition);
        }

        while (!grid.IsFullyCollapsed())
        {
            iterations++;

            if (iterations > MaxIterations)
            {
                return WfcSolveResult.Failed(
                    $"Exceeded maximum iterations ({MaxIterations})",
                    iterations);
            }

            // Find cell with lowest entropy (with random tie-breaking)
            var targetPos = grid.GetLowestEntropyCellWithTieBreak(rng);

            if (targetPos == null)
            {
                // All cells collapsed - we're done
                break;
            }

            var targetCell = grid.GetCell(targetPos.Value);

            // Check for contradiction before collapse
            if (targetCell.IsContradiction())
            {
                return WfcSolveResult.Failed(
                    "Found cell with no valid options",
                    iterations,
                    targetPos);
            }

            // Get tiles matching collapsed neighbors for continuity bias
            var continuityTiles = GetContinuityMatchingTiles(grid, targetPos.Value);

            // Select tile using weighted probabilities
            var selectedTile = _selector.SelectTile(
                targetCell.GetPossibleTiles(),
                biome,
                rng,
                continuityTiles,
                targetPos.Value,
                grid);

            if (selectedTile == null)
            {
                return WfcSolveResult.Failed(
                    "Tile selector returned null",
                    iterations,
                    targetPos);
            }

            // Collapse the cell
            targetCell.CollapseTo(selectedTile);

            // Update blob tracker for soft modifiers
            _blobTracker?.RegisterCollapse(targetPos.Value, selectedTile, grid);

            // Propagate constraints to neighbors
            var propResult = _propagator.Propagate(grid, targetPos.Value);
            if (!propResult.Success)
            {
                return WfcSolveResult.Failed(
                    $"Propagation failed at {propResult.ContradictionPosition}",
                    iterations,
                    propResult.ContradictionPosition);
            }
        }

        return WfcSolveResult.Succeeded(iterations);
    }

    /// <summary>
    /// Attempts to solve with automatic retry on contradiction.
    /// Each retry uses a fresh grid with a different random seed.
    /// </summary>
    /// <param name="createGrid">Factory function to create a fresh grid</param>
    /// <param name="biome">Biome for soft rule weighting</param>
    /// <param name="baseSeed">Base seed for random number generation</param>
    /// <param name="maxRetries">Maximum retry attempts (default 3)</param>
    /// <returns>Tuple of (result, finalGrid) - grid is the last attempt's grid</returns>
    public (WfcSolveResult result, WfcGrid grid) SolveWithRetry(
        System.Func<WfcGrid> createGrid,
        BiomeDefinition? biome,
        ulong baseSeed,
        int maxRetries = 3)
    {
        WfcGrid? lastGrid = null;
        WfcSolveResult lastResult = default;

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

        return (WfcSolveResult.Failed(
            $"All {maxRetries + 1} attempts failed. Last error: {lastResult.ErrorMessage}",
            lastResult.Iterations,
            lastResult.ContradictionPosition), lastGrid!);
    }

    /// <summary>
    /// Gets the set of tile IDs from collapsed neighbors at the given position.
    /// Used to apply continuity bias in tile selection.
    /// </summary>
    private static HashSet<string>? GetContinuityMatchingTiles(WfcGrid grid, Vector2I pos)
    {
        HashSet<string>? result = null;

        foreach (var neighborPos in grid.GetNeighbors(pos))
        {
            var neighborCell = grid.GetCell(neighborPos);
            if (neighborCell.IsCollapsed())
            {
                result ??= new HashSet<string>();
                result.Add(neighborCell.GetCollapsedTile());
            }
        }

        return result;
    }
}
