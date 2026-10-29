using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// The biome strength grid the WFC constraints read, without the class that builds it. Strength values are in the
/// range [-1, 1]. Lookups return 0 for an unknown biome or an out-of-range position.
/// </summary>
public interface IBiomeStrengthField
{
    /// <summary>Gets the biome strength at a position, or 0 for an unknown biome or an off-grid position.</summary>
    /// <param name="position">Grid position to query</param>
    /// <param name="biomeId">ID of the biome to check</param>
    /// <returns>Strength in [-1, 1] range, or 0 if biome is unknown or position is out of range</returns>
    float GetStrength(Vector2I position, string biomeId);
}
