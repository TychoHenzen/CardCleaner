using System;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Configuration for map generation.
/// </summary>
public class MapGenerationConfig
{
    /// <summary>
    /// Size of the map. For grid maps, this is the exact size. The mesh generator does not read it: its ring count
    /// comes from a MeshMapGenerationConfig, or its default for a plain config.
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
}
