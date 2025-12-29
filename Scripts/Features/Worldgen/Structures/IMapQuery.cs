using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Structures;

/// <summary>
/// Interface for querying map state during structure generation.
/// </summary>
public interface IMapQuery
{
    /// <summary>Get the map size.</summary>
    Vector2I Size { get; }

    /// <summary>Get the tile ID at a position, or null if empty/out of bounds.</summary>
    string? GetTileAt(Vector2I position);

    /// <summary>Check if a position is passable terrain.</summary>
    bool IsPassable(Vector2I position);

    /// <summary>Get the biome at a position.</summary>
    BiomeDefinition GetBiomeAt(Vector2I position);
}
