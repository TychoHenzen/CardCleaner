using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public abstract partial class BaselineGradient : Resource
{
    public abstract CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize);
}

[Tool]
[GlobalClass]
public partial class RadialGradient : BaselineGradient
{
    // Default values as constants
    private const float DefaultFalloff = 1.0f;
    private static readonly CardSignature DefaultCenterSignature = new();
    private static readonly CardSignature DefaultEdgeSignature = new();

    [Export] public CardSignature CenterSignature { get; set; } = new();
    [Export] public CardSignature EdgeSignature { get; set; } = new();
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
        var result = new CardSignature();
        for (var i = 0; i < 8; i++) result[i] = Mathf.Lerp(CenterSignature[i], EdgeSignature[i], t);
        return result;
    }
}

[Tool]
[GlobalClass]
public partial class NoiseGradient : BaselineGradient
{
    // Default values as constants
    private const float DefaultNoiseScale = 0.1f;
    private const float DefaultNoiseStrength = 0.3f;
    private const int DefaultNoiseSeed = 42;
    private static readonly CardSignature DefaultBaseSignature = new();

    [Export] public CardSignature BaseSignature { get; set; } = new();
    [Export] public float NoiseScale { get; set; } = DefaultNoiseScale;
    [Export] public float NoiseStrength { get; set; } = DefaultNoiseStrength;
    [Export] public int NoiseSeed { get; set; } = DefaultNoiseSeed;

    private FastNoiseLite? _noise;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(BaseSignature) => true,
            nameof(NoiseScale) => true,
            nameof(NoiseStrength) => true,
            nameof(NoiseSeed) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(BaseSignature) => Variant.From(DefaultBaseSignature),
            nameof(NoiseScale) => DefaultNoiseScale,
            nameof(NoiseStrength) => DefaultNoiseStrength,
            nameof(NoiseSeed) => DefaultNoiseSeed,
            _ => base._PropertyGetRevert(property)
        };
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
    {
        _noise ??= new FastNoiseLite { Seed = NoiseSeed, Frequency = NoiseScale };

        var result = new CardSignature();
        for (var i = 0; i < 8; i++)
        {
            var noiseValue = _noise.GetNoise2D(position.X + i * 100, position.Y + i * 100);
            result[i] = Mathf.Clamp(BaseSignature[i] + noiseValue * NoiseStrength, -1f, 1f);
        }

        return result;
    }
}