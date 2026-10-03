using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Interfaces;

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
