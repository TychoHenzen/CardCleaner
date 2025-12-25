using System;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     Service for resetting the player to a safe position.
///     Orchestrates the reset flow between safe position tracking and player controller.
/// </summary>
public interface IPlayerResetService
{
    /// <summary>
    ///     Indicates if a reset is currently on cooldown to prevent spam.
    /// </summary>
    bool IsOnCooldown { get; }

    /// <summary>
    ///     Resets the player to the last known safe position, or spawn if none available.
    /// </summary>
    /// <returns>True if reset was successful, false if no safe position available</returns>
    bool ResetPlayerToSafety();

    /// <summary>
    ///     Event raised when player is reset to a safe position.
    /// </summary>
    event Action? PlayerReset;
}
