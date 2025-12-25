using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
///     Tracks safe player positions for respawn functionality.
///     Uses a bounded queue to maintain recent safe positions with FIFO semantics.
/// </summary>
public class SafePositionTracker : ISafePositionTracker
{
    private const int MaxPositions = 10;
    private readonly Queue<Vector3> _safePositions = new();
    private Vector3? _spawnPosition;

    public int PositionCount => _safePositions.Count;

    public bool RecordSafePosition(Vector3 position)
    {
        if (!IsValidPosition(position))
        {
            ILog.Warning($"SafePositionTracker: Rejected invalid position {position}");
            return false;
        }

        // Enforce bounded queue - remove oldest when full
        while (_safePositions.Count >= MaxPositions) _safePositions.Dequeue();

        _safePositions.Enqueue(position);
        return true;
    }

    public Vector3? GetLastSafePosition()
    {
        if (_safePositions.Count == 0)
            return null;

        // Queue doesn't have direct access to last element, so we peek through the array
        var positions = _safePositions.ToArray();
        return positions[^1];
    }

    public Vector3? GetSpawnPosition() => _spawnPosition;

    public void SetSpawnPosition(Vector3 position)
    {
        if (!IsValidPosition(position))
        {
            ILog.Error($"SafePositionTracker: Cannot set invalid spawn position {position}");
            return;
        }

        _spawnPosition = position;
        ILog.Print($"SafePositionTracker: Spawn position set to {position}");
    }

    public void Clear()
    {
        _safePositions.Clear();
        _spawnPosition = null;
        ILog.Print("SafePositionTracker: Cleared all positions");
    }

    private static bool IsValidPosition(Vector3 position)
    {
        return float.IsFinite(position.X) &&
               float.IsFinite(position.Y) &&
               float.IsFinite(position.Z);
    }
}
