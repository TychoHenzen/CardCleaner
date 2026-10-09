namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Outcome of solving a cell graph. <c>CollapsedTiles</c> has one entry per cell, indexed by cell id, and holds
/// null for a cell that did not collapse. It is filled on failure too.
/// </summary>
public sealed record WfcGraphSolution(bool Success, int Iterations, string? ErrorMessage, string?[] CollapsedTiles);
