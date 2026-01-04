using System;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Thread-safe IProgress implementation that marshals progress updates to Godot's main thread via signals.
/// </summary>
/// <remarks>
/// This Node-based progress reporter ensures that progress updates from background threads
/// are safely delivered to the main thread using Godot's CallDeferred mechanism.
///
/// Lifecycle:
/// 1. Create and AddChild to scene tree before starting async operation
/// 2. Connect to ProgressUpdated signal
/// 3. Pass as IProgress parameter to async method
/// 4. QueueFree() when operation completes
///
/// Example:
/// <code>
/// var progress = new GodotProgress();
/// AddChild(progress);
/// progress.ProgressUpdated += OnProgressUpdate;
/// try {
///     await asyncOperation(progress, ct);
/// } finally {
///     progress.QueueFree();
/// }
/// </code>
/// </remarks>
public partial class GodotProgress : Node, IProgress<float>
{
    /// <summary>
    /// Emitted when progress value changes.
    /// Guaranteed to be emitted on the main thread.
    /// </summary>
    /// <param name="value">Progress value from 0.0 (start) to 1.0 (complete)</param>
    [Signal]
    public delegate void ProgressUpdatedEventHandler(float value);

    /// <summary>
    /// Reports a progress update.
    /// Can be called from any thread - will be marshaled to main thread via CallDeferred.
    /// </summary>
    /// <param name="value">Progress value from 0.0 to 1.0</param>
    public void Report(float value)
    {
        // CallDeferred ensures signal emission happens on main thread
        CallDeferred(MethodName.EmitSignal, SignalName.ProgressUpdated, value);
    }
}
