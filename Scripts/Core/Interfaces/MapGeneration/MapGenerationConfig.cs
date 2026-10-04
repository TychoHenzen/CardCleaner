using System;
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
