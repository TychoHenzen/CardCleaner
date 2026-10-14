using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class RadialGradient : BaselineGradient
{
    // Default values as constants
    private const float DefaultFalloff = 1.0f;
    private static readonly CardSignature DefaultCenterSignature = new();
    private static readonly CardSignature DefaultEdgeSignature = new();

    public CardSignature CenterSignature { get; set; } = new();
    public CardSignature EdgeSignature { get; set; } = new();
    [Export] public float Falloff { get; set; } = DefaultFalloff;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(CenterSignature) => true,
            nameof(EdgeSignature) => true,
            nameof(Falloff) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(CenterSignature) => Variant.From(DefaultCenterSignature),
            nameof(EdgeSignature) => Variant.From(DefaultEdgeSignature),
            nameof(Falloff) => DefaultFalloff,
            _ => base._PropertyGetRevert(property)
        };
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
    {
        var center = new Vector2(mapSize.X / 2f, mapSize.Y / 2f);
        var pos = new Vector2(position.X, position.Y);
        var maxDistance = center.Length();
        var distance = pos.DistanceTo(center);
        var normalizedDistance = Mathf.Clamp(distance / maxDistance, 0f, 1f);

        // Apply falloff curve
        var t = Mathf.Pow(normalizedDistance, Falloff);

        // Blend signatures
        var elements = new float[8];
        for (var i = 0; i < 8; i++) elements[i] = Mathf.Lerp(CenterSignature[i], EdgeSignature[i], t);
        return new CardSignature(elements);
    }
}
