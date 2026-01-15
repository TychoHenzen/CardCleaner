using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Biomes;

/// <summary>
/// Card-based signature gradient for mesh vertex positions.
/// Creates spatial variation in biome influence based on input cards.
/// Uses CardGradientCore for the actual gradient generation.
/// </summary>
public class MeshCardBasedGradient
{
    private readonly CardGradientCore _core;
    private readonly Vector2 _mapBounds;

    /// <summary>
    /// Creates a gradient from input cards.
    /// </summary>
    /// <param name="inputCards">The cards to use for gradient generation.</param>
    /// <param name="mapBounds">The size of the map (for normalizing positions).</param>
    /// <param name="rng">Random number generator for sampling.</param>
    public MeshCardBasedGradient(CardSignature[] inputCards, Vector2 mapBounds, RandomNumberGenerator rng)
    {
        _mapBounds = mapBounds;
        _core = new CardGradientCore(inputCards, rng);
    }

    /// <summary>
    /// Gets the signature at a world position.
    /// </summary>
    /// <param name="position">World position (x, y).</param>
    /// <returns>Interpolated signature at the position.</returns>
    public CardSignature GetSignatureAt(Vector2 position)
    {
        if (_mapBounds.X == 0 || _mapBounds.Y == 0)
            return new CardSignature();

        var normalizedX = Mathf.Clamp(position.X / _mapBounds.X, 0, 1);
        var normalizedY = Mathf.Clamp(position.Y / _mapBounds.Y, 0, 1);

        return _core.GetSignatureAtNormalized(normalizedX, normalizedY);
    }

    /// <summary>
    /// Regenerates the sample grid (call after changing cards).
    /// </summary>
    public void Regenerate()
    {
        _core.MarkForRegeneration();
    }
}
