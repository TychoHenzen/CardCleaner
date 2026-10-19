using System;
using System.Collections.Generic;
using System.Threading;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// The front door the grid map pipeline uses to run WFC. The tuning properties and the single-biome Generate stay
/// on <see cref="WfcMapGenerator"/>.
/// </summary>
public interface IWfcMapGenerator
{
    /// <summary>
    /// How many times a solve is retried with a derived seed before it fails.
    /// </summary>
    int MaxRetries { get; set; }

    /// <summary>
    /// Sets the tile variant picked for each variant group, keyed by group base name.
    /// </summary>
    void SetSelectedVariants(Dictionary<string, string>? selectedVariants);

    /// <summary>
    /// Generates a map whose tiles follow the biome each cell falls in, optionally biased by a strength field.
    /// </summary>
    WfcGenerationResult GenerateMultiBiome(
        BiomeSet biomes,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        Vector2I size,
        ulong seed,
        BiomeStrengthField? strengths = null,
        Func<string, bool>? tileFilter = null,
        CancellationToken cancellationToken = default);
}
