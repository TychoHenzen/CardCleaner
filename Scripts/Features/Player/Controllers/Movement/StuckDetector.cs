using Godot;

namespace CardCleaner.Scripts.Features.Player.Controllers;

/// <summary>
/// Detects a player who keeps trying to move without actually going anywhere over a fixed time window.
/// </summary>
internal sealed class StuckDetector
{
    private const float DetectionWindow = 5.0f; // Time window to detect stuck state
    private const float MovementThreshold = 0.5f; // Minimum cumulative movement expected in window

    private Vector3 _lastPosition;
    private float _cumulativeMovement;
    private float _timeWithMovementInput;
    private float _timer;

    /// <summary>
    /// Records one physics step. Returns true once per window when the player was trying to move for
    /// most of it but barely moved.
    /// </summary>
    internal bool Update(Vector3 position, float delta, bool hasMovementInput)
    {
        // Track movement delta
        _cumulativeMovement += position.DistanceTo(_lastPosition);
        _lastPosition = position;

        // Only count time when player is actively trying to move
        if (hasMovementInput)
            _timeWithMovementInput += delta;

        _timer += delta;

        // Check if detection window has elapsed
        if (_timer < DetectionWindow)
            return false;

        // Only trigger stuck if player was trying to move for most of the window
        // and didn't actually move much
        var wasActivelyTryingToMove = _timeWithMovementInput > DetectionWindow * 0.8f;
        var isStuck = wasActivelyTryingToMove && _cumulativeMovement < MovementThreshold;

        // Reset tracking for next window
        _timer = 0f;
        _cumulativeMovement = 0f;
        _timeWithMovementInput = 0f;

        return isStuck;
    }
}
