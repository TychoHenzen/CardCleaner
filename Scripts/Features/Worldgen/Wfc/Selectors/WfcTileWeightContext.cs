using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Selectors;

/// <summary>
/// Inputs shared by the weight-building collaborators for one tile selection.
/// </summary>
internal readonly record struct WfcTileWeightContext(
    Dictionary<string, float> BiomeWeights,
    RandomNumberGenerator Rng,
    IReadOnlySet<string>? ContinuityTiles,
    int? CellId,
    IWfcTopology? Topology,
    Dictionary<int, string>? CollapsedNeighbors,
    bool UseUniformBaseWeight,
    IReadOnlyList<IWfcConstraint> Constraints,
    float DefaultTileWeight,
    float NonBiomeTilePenalty,
    float ContinuityBiasMultiplier);
