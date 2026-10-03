using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.BiomeGridScenarios;

/// <summary>
/// Test gradient that returns a fixed signature for all positions.
/// </summary>
internal sealed partial class FixedGradient : BaselineGradient
{
    private readonly CardSignature _signature;

    public FixedGradient(CardSignature signature)
    {
        _signature = signature;
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
    {
        return _signature;
    }
}
