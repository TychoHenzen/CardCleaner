using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Structures;

/// <summary>
/// Interface for algorithm-generated structures that produce tile patterns dynamically.
/// </summary>
public interface IProceduralStructure
{
    /// <summary>Unique identifier for this structure type.</summary>
    string Id { get; }

    /// <summary>
    /// Generate a structure at the given position.
    /// </summary>
    /// <param name="position">Anchor position (top-left) for the structure.</param>
    /// <param name="seed">Seed for deterministic generation.</param>
    /// <param name="biome">Biome at the structure position.</param>
    /// <param name="mapQuery">Interface to query current map state.</param>
    /// <returns>Structure result containing tiles and influence data.</returns>
    StructureResult Generate(
        Vector2I position,
        int seed,
        BiomeDefinition biome,
        IMapQuery mapQuery);
}
