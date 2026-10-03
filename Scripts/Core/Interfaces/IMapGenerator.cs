using System;
using System.Threading;
using System.Threading.Tasks;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Interface for map generators that produce playable maps.
/// </summary>
public interface IMapGenerator
{
    /// <summary>
    /// Generates a map asynchronously.
    /// </summary>
    /// <param name="config">Generation configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated map result.</returns>
    Task<IGeneratedMap> GenerateAsync(
        MapGenerationConfig config,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default);
}
