using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Support;

/// <summary>
///     Gradient that returns one signature on the left half of the map and another on the right half.
/// </summary>
public sealed partial class TwoZoneGradient : BaselineGradient
{
    private readonly CardSignature _left;
    private readonly CardSignature _right;

    public TwoZoneGradient(CardSignature left, CardSignature right)
    {
        _left = left;
        _right = right;
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize) =>
        position.X < mapSize.X / 2 ? _left : _right;
}
