using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Inputs that stay fixed for the whole of one solve.
/// </summary>
internal readonly record struct WfcSolveRun(
    IWfcTopology Topology,
    BiomeDefinition? Biome,
    IRandom Rng,
    int TotalCells);
