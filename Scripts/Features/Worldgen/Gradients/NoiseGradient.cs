using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Numerics;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

public class NoiseGradient(Func<int, float, INoiseSource> noiseSourceFactory) : BaselineGradient
{
    public CardSignature BaseSignature { get; set; } = new();
    public float NoiseScale { get; set; } = 0.1f;
    public float NoiseStrength { get; set; } = 0.3f;
    public int NoiseSeed { get; set; } = 42;

    private INoiseSource? _noise;

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
    {
        _noise ??= noiseSourceFactory(NoiseSeed, NoiseScale);

        var elements = new float[8];
        for (var i = 0; i < 8; i++)
        {
            var noiseValue = _noise.GetNoise2D(position.X + i * 100, position.Y + i * 100);
            elements[i] = CoreMath.Clamp(BaseSignature[i] + noiseValue * NoiseStrength, -1f, 1f);
        }

        return new CardSignature(elements);
    }
}
