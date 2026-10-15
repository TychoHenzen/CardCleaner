using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

public readonly struct WfcGenerationResult
{
    public bool Success { get; }
    public Vector2I Size { get; }
    public string[,]? TileIds { get; }
    public string[,]? BiomeMap { get; }
    public int Iterations { get; }
    public string? ErrorMessage { get; }
    public WfcStepTimings Timings { get; }

    private WfcGenerationResult(
        bool success,
        Vector2I size,
        string[,]? tileIds,
        string[,]? biomeMap,
        int iterations,
        string? error,
        WfcStepTimings timings)
    {
        Success = success;
        Size = size;
        TileIds = tileIds;
        BiomeMap = biomeMap;
        Iterations = iterations;
        ErrorMessage = error;
        Timings = timings;
    }

    public static WfcGenerationResult Succeeded(
        Vector2I size,
        string[,] tileIds,
        string[,] biomeMap,
        int iterations,
        WfcStepTimings timings = default) =>
        new(true, size, tileIds, biomeMap, iterations, null, timings);

    /// <summary>A failed generation carries zero timings.</summary>
    public static WfcGenerationResult Failed(string error) =>
        new(false, Vector2I.Zero, null, null, 0, error, WfcStepTimings.Zero);
}
