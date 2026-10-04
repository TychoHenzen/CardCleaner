using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Support;

/// <summary>
///     Simple gradient that returns a constant signature at all positions.
/// </summary>
public sealed partial class ConstantSignatureGradient : BaselineGradient
{
    private readonly CardSignature _signature;

    public ConstantSignatureGradient(CardSignature signature)
    {
        _signature = signature;
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize) => _signature;
}
