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

    private WfcGenerationResult(
        bool success,
        Vector2I size,
        string[,]? tileIds,
        string[,]? biomeMap,
        int iterations,
        string? error)
    {
        Success = success;
        Size = size;
        TileIds = tileIds;
        BiomeMap = biomeMap;
        Iterations = iterations;
        ErrorMessage = error;
    }

    public static WfcGenerationResult Succeeded(
        Vector2I size,
        string[,] tileIds,
        string[,] biomeMap,
        int iterations) =>
        new(true, size, tileIds, biomeMap, iterations, null);

    public static WfcGenerationResult Failed(string error) =>
        new(false, Vector2I.Zero, null, null, 0, error);
}
