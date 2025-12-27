using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Transitions;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Interface for the terrain transition system.
/// Provides access to terrain groups, transition rules, and transition calculation.
/// </summary>
public interface ITransitionRegistry
{
    /// <summary>
    /// Get the terrain group that contains a specific tile ID
    /// </summary>
    TerrainGroup? GetGroupForTile(string tileId);

    /// <summary>
    /// Get the transition tile ID to render for a position.
    /// Returns null if no transition is needed.
    /// </summary>
    string? GetTransitionTileId(Vector2I position, string[,] tileIds, Vector2I mapSize);

    /// <summary>
    /// Calculate the 4-bit transition bitmask for a position.
    /// Bitmask bits: N=1, E=2, S=4, W=8
    /// </summary>
    int CalculateBitmask(Vector2I position, string[,] tileIds, Vector2I mapSize);

    /// <summary>
    /// Get the blob generation configuration
    /// </summary>
    BlobGenerationConfig BlobConfig { get; }

    /// <summary>
    /// Get the terrain group registry for blob generation
    /// </summary>
    TerrainGroupRegistry GroupRegistry { get; }
}
