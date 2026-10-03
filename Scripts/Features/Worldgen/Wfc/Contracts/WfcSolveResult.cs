namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Result of a WFC solve operation.
/// </summary>
public readonly struct WfcSolveResult
{
    public bool Success { get; }
    public int Iterations { get; }
    public string? ErrorMessage { get; }
    public int? ContradictionCellId { get; }

    private WfcSolveResult(bool success, int iterations, string? error = null, int? contradictionCellId = null)
    {
        Success = success;
        Iterations = iterations;
        ErrorMessage = error;
        ContradictionCellId = contradictionCellId;
    }

    public static WfcSolveResult Succeeded(int iterations) => new(true, iterations);
    public static WfcSolveResult Failed(string error, int iterations, int? cellId = null) =>
        new(false, iterations, error, cellId);
}
