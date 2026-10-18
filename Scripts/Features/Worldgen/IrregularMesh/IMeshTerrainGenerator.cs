using System;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates the terrain of an irregular mesh map from a signature sampler.
/// </summary>
public interface IMeshTerrainGenerator
{
    IrregularMesh GenerateWithSampler(
        int rings,
        Func<Vector2I, Vector2I, CardSignature>? signatureAt,
        int seed,
        int relaxationIterations = 15,
        bool useConstraints = true);
}
