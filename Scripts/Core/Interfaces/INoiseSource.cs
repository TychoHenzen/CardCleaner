namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>A source of 2D noise in [-1, 1]; the engine-free seam over FastNoiseLite.</summary>
public interface INoiseSource
{
    float GetNoise2D(float x, float y);
}
