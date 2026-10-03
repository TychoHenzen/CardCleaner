using System.Threading;
using System.Threading.Tasks;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;

/// <summary>
/// Tracks which map generation attempt is current so superseded attempts can be discarded.
/// </summary>
internal sealed class GenerationTracker
{
    private long _generationId;
    private long _pendingGenerationId;
    private CancellationTokenSource? _generationCts;
    private TaskCompletionSource<bool>? _pendingGenerationTaskSource;

    /// <summary>
    /// The task of the current generation, awaited by tests and callers that need completion.
    /// </summary>
    internal Task? CurrentTask { get; set; }

    /// <summary>
    /// Registers a generation that will start on the next deferred call and returns its id.
    /// </summary>
    internal long BeginPending()
    {
        CurrentTask = null; // Clear any stale task from previous session
        var generationId = ++_generationId;
        _pendingGenerationId = generationId;
        _pendingGenerationTaskSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CurrentTask = _pendingGenerationTaskSource.Task;
        return generationId;
    }

    /// <summary>
    /// Takes the pending completion source when it belongs to the given generation.
    /// </summary>
    internal TaskCompletionSource<bool>? TakePending(long generationId)
    {
        if (_pendingGenerationId != generationId || _pendingGenerationTaskSource == null)
            return null;

        var source = _pendingGenerationTaskSource;
        _pendingGenerationTaskSource = null;
        _pendingGenerationId = 0;
        return source;
    }

    /// <summary>
    /// Starts a new generation, cancelling the previous one.
    /// </summary>
    internal GenerationRun BeginRun()
    {
        var generationId = ++_generationId;
        var generationCts = new CancellationTokenSource();
        var previousGenerationCts = _generationCts;
        _generationCts = generationCts;
        previousGenerationCts?.Cancel();
        return new GenerationRun(generationId, generationCts);
    }

    internal bool IsCurrent(long generationId, CancellationTokenSource? generationCts = null)
    {
        if (_generationId != generationId)
            return false;

        if (generationCts != null && !ReferenceEquals(_generationCts, generationCts))
            return false;

        return generationCts == null || !generationCts.IsCancellationRequested;
    }

    internal void Release(CancellationTokenSource generationCts)
    {
        if (ReferenceEquals(_generationCts, generationCts))
            _generationCts = null;

        generationCts.Dispose();
    }

    /// <summary>
    /// Invalidates every generation and cancels the one in flight.
    /// </summary>
    internal void Reset()
    {
        _generationId++;
        _pendingGenerationTaskSource?.TrySetCanceled();
        _pendingGenerationTaskSource = null;
        _pendingGenerationId = 0;
        var generationCts = _generationCts;
        _generationCts = null;
        generationCts?.Cancel();
        CurrentTask = null;
    }
}
