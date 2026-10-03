using System;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Configuration for map generation.
/// </summary>
public class MapGenerationConfig
{
    /// <summary>
    /// Size hint for the map. For grid maps, this is exact size.
    /// For mesh maps, this influences the number of rings/vertices.
    /// </summary>
    public Vector2I Size { get; init; }

    /// <summary>
    /// Random seed for deterministic generation.
    /// </summary>
    public ulong Seed { get; init; }

    /// <summary>
    /// Card signatures used to influence biome distribution.
    /// </summary>
    public CardSignature[] MapSeeds { get; init; } = Array.Empty<CardSignature>();

    /// <summary>
    /// Biome registry for terrain selection.
    /// </summary>
    public BiomeRegistry? BiomeRegistry { get; init; }
}

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

/// <summary>
/// Interface for map generators that produce playable maps.
/// </summary>
public interface IMapGenerator
{
    /// <summary>
    /// Generates a map asynchronously.
    /// </summary>
    /// <param name="config">Generation configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated map result.</returns>
    Task<IGeneratedMap> GenerateAsync(
        MapGenerationConfig config,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default);
}
