using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Interface for checking line-of-sight visibility between positions.
/// Works with any IMapData implementation.
/// </summary>
public interface IVisibilityChecker
{
    /// <summary>Checks whether there is a clear line of sight between two cells.</summary>
    bool CanSee(int fromCellId, int toCellId, IMapData mapData);

    /// <summary>Checks whether there is a clear line of sight between two world positions.</summary>
    bool CanSee(Vector2 from, Vector2 to, IMapData mapData);
}
