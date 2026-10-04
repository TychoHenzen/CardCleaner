namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Result of a propagation operation.
/// </summary>
public readonly struct PropagationResult
{
    public bool Success { get; }
    public int? ContradictionCellId { get; }
    public int CellsUpdated { get; }

    public PropagationResult(bool success, int cellsUpdated, int? contradictionCellId = null)
    {
        Success = success;
        CellsUpdated = cellsUpdated;
        ContradictionCellId = contradictionCellId;
    }

    public static PropagationResult Succeeded(int cellsUpdated) => new(true, cellsUpdated);
    public static PropagationResult Failed(int cellId) => new(false, 0, cellId);
}
