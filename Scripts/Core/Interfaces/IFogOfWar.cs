using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Visibility state for fog of war cells.
/// </summary>
public enum FogState
{
    /// <summary>Never been seen - completely hidden.</summary>
    Hidden,

    /// <summary>Previously seen but not currently visible - shown dimmed.</summary>
    Revealed,

    /// <summary>Currently visible - shown fully.</summary>
    Visible
}

/// <summary>
/// Interface for fog of war state management.
/// Implementations track which cells have been seen/revealed/visible.
/// </summary>
public interface IFogOfWar
{
    /// <summary>
    /// Get the fog state for a cell.
    /// </summary>
    FogState GetFogState(int cellId);

    /// <summary>
    /// Check if a cell is currently visible.
    /// </summary>
    bool IsVisible(int cellId);

    /// <summary>
    /// Check if a cell has been seen (revealed or visible).
    /// </summary>
    bool HasBeenSeen(int cellId);

    /// <summary>
    /// Currently visible cell IDs.
    /// </summary>
    IReadOnlySet<int> CurrentlyVisibleCells { get; }

    /// <summary>
    /// All cells that have ever been seen.
    /// </summary>
    IReadOnlySet<int> SeenCells { get; }

    /// <summary>
    /// Reveal all cells (disable fog of war).
    /// </summary>
    void RevealAll();

    /// <summary>
    /// Reset all cells to hidden state.
    /// </summary>
    void Reset();

    /// <summary>
    /// Raised when visibility state changes.
    /// Parameter: set of cell IDs that changed.
    /// </summary>
    event Action<IReadOnlySet<int>>? VisibilityChanged;
}

/// <summary>
/// Extended fog of war interface for active visibility calculation.
/// Implementations calculate visibility from an observer position.
/// </summary>
public interface IActiveFogOfWar : IFogOfWar
{
    /// <summary>
    /// Update visibility from an observer cell position.
    /// </summary>
    void UpdateVisibility(int observerCellId);

    /// <summary>
    /// Vision range in world units.
    /// </summary>
    float VisionRange { get; set; }
}

/// <summary>
/// Extended fog of war interface for passive visibility updates.
/// Implementations receive pre-computed visibility sets from external sources.
/// </summary>
public interface IPassiveFogOfWar : IFogOfWar
{
    /// <summary>
    /// Update visibility based on externally computed seen and visible sets.
    /// </summary>
    /// <param name="seenCellIds">All cells that have been seen.</param>
    /// <param name="visibleCellIds">Currently visible cells.</param>
    void UpdateVisibility(IReadOnlySet<int> seenCellIds, IReadOnlySet<int> visibleCellIds);
}
