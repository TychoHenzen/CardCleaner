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
    public WfcStepTimings Timings { get; }

    private WfcSolveResult(
        bool success,
        int iterations,
        string? error,
        int? contradictionCellId,
        WfcStepTimings timings)
    {
        Success = success;
        Iterations = iterations;
        ErrorMessage = error;
        ContradictionCellId = contradictionCellId;
        Timings = timings;
    }

    public static WfcSolveResult Succeeded(int iterations, WfcStepTimings timings = default) =>
        new(true, iterations, null, null, timings);

    public static WfcSolveResult Failed(
        string error, int iterations, int? cellId = null, WfcStepTimings timings = default) =>
        new(false, iterations, error, cellId, timings);

    public WfcSolveResult WithTimings(WfcStepTimings timings) =>
        new(Success, Iterations, ErrorMessage, ContradictionCellId, timings);
}
