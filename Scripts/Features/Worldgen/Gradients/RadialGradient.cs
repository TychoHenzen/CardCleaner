using CardCleaner.Scripts.Core.Numerics;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public class RadialGradient : BaselineGradient
{
    // Default values as constants
    private const float DefaultFalloff = 1.0f;

    public CardSignature CenterSignature { get; set; } = new();
    public CardSignature EdgeSignature { get; set; } = new();
    public float Falloff { get; set; } = DefaultFalloff;

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
    {
        var center = new Vector2(mapSize.X / 2f, mapSize.Y / 2f);
        var pos = new Vector2(position.X, position.Y);
        var maxDistance = center.Length();
        var distance = pos.DistanceTo(center);
        var normalizedDistance = CoreMath.Clamp(distance / maxDistance, 0f, 1f);

        // Apply falloff curve
        var t = CoreMath.Pow(normalizedDistance, Falloff);

        // Blend signatures
        var elements = new float[8];
        for (var i = 0; i < 8; i++) elements[i] = CoreMath.Lerp(CenterSignature[i], EdgeSignature[i], t);
        return new CardSignature(elements);
    }
}
