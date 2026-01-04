using System;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Adapter that wraps SimpleMapGenerator to provide asynchronous generation with progress reporting.
/// </summary>
/// <remarks>
/// This adapter runs SimpleMapGenerator on a background thread using Task.Run.
/// Progress reporting is achieved by injecting a CancellableProgressProfiler that reports progress
/// based on completion of profiled generation phases.
///
/// Cancellation is handled by checking CancellationToken at phase boundaries. Note that cancellation
/// granularity depends on profiling scope placement - cancellation during a long-running phase
/// (like WFC solve) may take time to respond.
/// </remarks>
public class AsyncMapGeneratorAdapter : IAsyncMapGenerator
{
    private readonly SimpleMapGenerator _generator;

    public AsyncMapGeneratorAdapter(SimpleMapGenerator generator)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    /// <summary>
    /// Generates a map asynchronously on a background thread.
    /// </summary>
    /// <param name="size">Dimensions of the map to generate</param>
    /// <param name="progress">Optional progress reporter (0.0 = start, 1.0 = complete)</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Generated map data</returns>
    /// <exception cref="MapGenerationCancelledException">Thrown when generation is cancelled</exception>
    public async Task<SimpleMapData> GenerateMapAsync(
        Vector2I size,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Create profiler that reports progress and checks cancellation
        var profiler = new CancellableProgressProfiler(progress, cancellationToken);
        _generator.SetProfiler(profiler);

        try
        {
            // Run generation on background thread
            return await Task.Run(() => _generator.GenerateMap(size), cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            // Wrap in domain-specific exception
            throw new MapGenerationCancelledException("Map generation was cancelled by user", ex);
        }
    }
}
