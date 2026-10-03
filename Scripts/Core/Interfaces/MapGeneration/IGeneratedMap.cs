using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Result of map generation containing the map data adapter.
/// </summary>
public interface IGeneratedMap
{
    /// <summary>
    /// Gets the map data adapter for pathfinding, exploration, etc.
    /// </summary>
    IMapData GetMapData();

    /// <summary>
    /// Number of enemies on the map.
    /// </summary>
    int EnemyCount { get; }

    /// <summary>Removes an enemy at the specified cell.</summary>
    bool RemoveEnemyAt(int cellId);

    /// <summary>
    /// Gets the player start position in world coordinates.
    /// </summary>
    Vector2 PlayerStartPosition { get; }
}
