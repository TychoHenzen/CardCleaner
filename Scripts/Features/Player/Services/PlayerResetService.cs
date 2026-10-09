using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Player.Services;

/// <summary>
///     Orchestrates player safety resets by coordinating between position tracker and player controller.
///     Includes cooldown to prevent reset spam.
/// </summary>
public partial class PlayerResetService : Node, IPlayerResetService
{
    private const float ResetCooldownDuration = 2.0f;
    private float _cooldownRemaining;
    private PlayerController? _playerController;

    private ISafePositionTracker? _positionTracker;

    public event Action? PlayerReset;

    public bool IsOnCooldown => _cooldownRemaining > 0;

    public bool ResetPlayerToSafety()
    {
        if (IsOnCooldown)
        {
            ILog.Warning("PlayerResetService: Reset on cooldown, ignoring request");
            return false;
        }

        // Lazy-find player controller
        _playerController ??= FindPlayerController();

        if (_playerController == null)
        {
            ILog.Error("PlayerResetService: Cannot find player controller");
            return false;
        }

        if (_positionTracker == null)
        {
            ILog.Error("PlayerResetService: Position tracker not available");
            return false;
        }

        // Try last safe position first, then fall back to spawn
        var targetPosition = _positionTracker.GetLastSafePosition()
                             ?? _positionTracker.GetSpawnPosition();

        if (targetPosition == null)
        {
            ILog.Error("PlayerResetService: No safe position or spawn position available");
            return false;
        }

        _playerController.ResetToPosition(targetPosition.Value);
        _cooldownRemaining = ResetCooldownDuration;

        ILog.Print($"PlayerResetService: Player reset to {targetPosition.Value}");
        PlayerReset?.Invoke();

        return true;
    }

    public override void _Ready() => ServiceLocator.Get<ISafePositionTracker>(tracker => _positionTracker = tracker);

    public override void _Process(double delta)
    {
        if (_cooldownRemaining > 0)
            _cooldownRemaining -= (float)delta;
    }

    private PlayerController? FindPlayerController()
    {
        var playerNode = GetTree().GetFirstNodeInGroup("player");
        return playerNode as PlayerController;
    }
}
