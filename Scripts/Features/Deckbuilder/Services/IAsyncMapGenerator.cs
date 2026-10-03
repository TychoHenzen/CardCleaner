using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Interface for asynchronous map generation with progress reporting and cancellation support.
/// </summary>
/// <remarks>
/// Progress reporting granularity depends on implementation. Implementations wrapping synchronous
/// generators may only report coarse-grained progress (start, complete). For fine-grained progress,
/// the underlying generator must support incremental reporting.
/// </remarks>
public interface IAsyncMapGenerator
{
    /// <summary>
    /// Generates a map asynchronously.
    /// </summary>
    /// <param name="size">Dimensions of the map to generate</param>
    /// <param name="progress">Optional progress reporter (0.0 = start, 1.0 = complete)</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Generated map data</returns>
    /// <exception cref="MapGenerationCancelledException">Thrown when generation is cancelled</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    Task<SimpleMapData> GenerateMapAsync(
        Vector2I size,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default);
}
