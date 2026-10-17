using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Utilities;

/// <summary>FastNoiseLite behind INoiseSource. Create is a stateless static factory on the class.</summary>
internal sealed class GodotNoiseSource : INoiseSource
{
    private readonly FastNoiseLite _noise;

    private GodotNoiseSource(int seed, float frequency) =>
        _noise = new FastNoiseLite { Seed = seed, Frequency = frequency };

    public static INoiseSource Create(int seed, float frequency) => new GodotNoiseSource(seed, frequency);

    public float GetNoise2D(float x, float y) => _noise.GetNoise2D(x, y);
}
