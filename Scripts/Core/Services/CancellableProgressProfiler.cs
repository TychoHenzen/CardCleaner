using System;
using System.Collections.Generic;
using System.Threading;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Profiler that reports progress and checks for cancellation at phase boundaries.
/// </summary>
/// <remarks>
/// This profiler serves dual purposes:
/// 1. Reports generation progress based on weighted phase completion
/// 2. Injects cancellation checks at every profiling scope entry
///
/// Progress weights are calibrated estimates and may not perfectly reflect actual time distribution.
/// Cancellation granularity depends on profiling scope placement.
/// </remarks>
public class CancellableProgressProfiler : IProfiler
{
    private readonly IProgress<float>? _progress;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<string, float> _phaseWeights;
    private readonly object _lock = new();
    private float _accumulatedProgress;

    /// <summary>
    /// Creates a profiler that reports progress and checks cancellation.
    /// </summary>
    /// <param name="progress">Optional progress reporter (0.0-1.0)</param>
    /// <param name="cancellationToken">Token to check for cancellation requests</param>
    /// <param name="phaseWeights">Optional custom phase weights (defaults provided)</param>
    public CancellableProgressProfiler(
        IProgress<float>? progress,
        CancellationToken cancellationToken,
        Dictionary<string, float>? phaseWeights = null)
    {
        _progress = progress;
        _cancellationToken = cancellationToken;
        _phaseWeights = phaseWeights ?? GetDefaultPhaseWeights();
        _accumulatedProgress = 0.0f;
    }

    private static Dictionary<string, float> GetDefaultPhaseWeights()
    {
        return new Dictionary<string, float>
        {
            ["BiomeMapBuild"] = 0.10f,
            ["WfcTerrainGeneration"] = 0.75f,
            ["VariantSelection"] = 0.05f,
            ["PassableCalculation"] = 0.10f
        };
    }

    public IDisposable BeginScope(string name)
    {
        // Check cancellation at every phase boundary
        _cancellationToken.ThrowIfCancellationRequested();

        return new ScopedTimer(name, OnScopeComplete);
    }

    private void OnScopeComplete(string name, long milliseconds)
    {
        if (_phaseWeights.TryGetValue(name, out var weight))
        {
            lock (_lock)
            {
                _accumulatedProgress += weight;
                var clampedProgress = Math.Min(_accumulatedProgress, 1.0f);
                _progress?.Report(clampedProgress);
            }
        }
    }

    public void RecordDuration(string name, long milliseconds)
    {
        // Not needed for progress reporting
    }

    public Dictionary<string, ProfileStats> GetStatistics()
    {
        // Return empty stats - we're focused on progress, not profiling
        return new Dictionary<string, ProfileStats>();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _accumulatedProgress = 0.0f;
        }
    }

    private class ScopedTimer : IDisposable
    {
        private readonly string _name;
        private readonly Action<string, long> _onComplete;
        private readonly System.Diagnostics.Stopwatch _stopwatch;

        public ScopedTimer(string name, Action<string, long> onComplete)
        {
            _name = name;
            _onComplete = onComplete;
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _onComplete(_name, _stopwatch.ElapsedMilliseconds);
        }
    }
}
