using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Interface for checking line-of-sight visibility between positions.
/// Works with any IMapData implementation.
/// </summary>
public interface IVisibilityChecker
{
    /// <summary>
    /// Check if there's a clear line of sight between two cells.
    /// </summary>
    /// <param name="fromCellId">Source cell ID.</param>
    /// <param name="toCellId">Target cell ID.</param>
    /// <param name="mapData">Map data for transparency checks.</param>
    /// <returns>True if the target is visible from the source.</returns>
    bool CanSee(int fromCellId, int toCellId, IMapData mapData);

    /// <summary>
    /// Check if there's a clear line of sight between two world positions.
    /// </summary>
    /// <param name="from">Source world position.</param>
    /// <param name="to">Target world position.</param>
    /// <param name="mapData">Map data for transparency checks.</param>
    /// <returns>True if the target is visible from the source.</returns>
    bool CanSee(Vector2 from, Vector2 to, IMapData mapData);
}
