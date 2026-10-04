using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     Tracks safe positions for player respawn/reset functionality.
///     Records positions during normal gameplay and provides retrieval for safety resets.
/// </summary>
public interface ISafePositionTracker
{
    /// <summary>
    ///     Number of safe positions currently tracked.
    /// </summary>
    int PositionCount { get; }

    /// <summary>Records a finite position as safe.</summary>
    bool RecordSafePosition(Vector3 position);

    /// <summary>
    ///     Gets the most recently recorded safe position.
    /// </summary>
    /// <returns>The last safe position, or null if none recorded</returns>
    Vector3? GetLastSafePosition();

    /// <summary>
    ///     Gets the designated spawn position (initial safe position).
    /// </summary>
    /// <returns>The spawn position, or null if not set</returns>
    Vector3? GetSpawnPosition();

    /// <summary>
    ///     Sets the spawn position explicitly. Called when map generates.
    /// </summary>
    /// <param name="position">The spawn/start position</param>
    void SetSpawnPosition(Vector3 position);

    /// <summary>
    ///     Clears all recorded positions. Called on session reset.
    /// </summary>
    void Clear();
}
