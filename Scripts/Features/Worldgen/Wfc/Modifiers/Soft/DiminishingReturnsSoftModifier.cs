using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>
/// Applies diminishing returns to tile weights based on connected blob size.
/// Counterbalances continuity bias to prevent single tile type domination.
///
/// Formula: multiplier = 1 / (1 + potentialBlobSize * DecayFactor)
///
/// With DecayFactor=0.2 (balanced with SpatialCoherenceConstraint):
/// - Blob size 5:   1 / (1 + 5 * 0.2) = 0.5 (allows coherent growth)
/// - Blob size 10:  1 / (1 + 10 * 0.2) = 0.33 (moderate decay)
/// - Blob size 20:  1 / (1 + 20 * 0.2) = 0.2 (target size, significant decay)
/// - Blob size 30:  1 / (1 + 30 * 0.2) = 0.14 (prevents domination)
///
/// Net effect with SpatialCoherence (BoostFactor=8.0) at size 20: ~5x × 0.2 = 1x (balanced)
/// </summary>
public class DiminishingReturnsSoftModifier : IWfcConstraint
{
    private readonly BlobSizeTracker _blobTracker;

    /// <summary>
    /// Decay factor controlling how quickly weights diminish.
    /// Higher values = faster decay, smaller blobs.
    /// With 0.2: Meaningful decay at 10-20 tiles, preventing single-type domination
    /// while allowing spatial coherence to form coherent regions.
    /// Formula results: size 5 = 0.5x, size 10 = 0.33x, size 20 = 0.2x
    /// </summary>
    public float DecayFactor { get; set; } = 0.2f;

    /// <summary>
    /// Minimum blob size before decay applies.
    /// Blobs smaller than this get no penalty.
    /// </summary>
    public int MinimumBlobSize { get; set; } = 1;

    /// <summary>
    /// Minimum multiplier to prevent weights from reaching zero.
    /// </summary>
    public float MinimumMultiplier { get; set; } = 0.01f;

    public DiminishingReturnsSoftModifier(BlobSizeTracker blobTracker)
    {
        _blobTracker = blobTracker;
    }

    /// <inheritdoc />
    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        var potentialSize = _blobTracker.GetPotentialBlobSize(
            context.Position,
            context.TileId,
            context.Grid);

        if (potentialSize < MinimumBlobSize)
            return 1.0f;

        // Formula: 1 / (1 + size * decay)
        var multiplier = 1.0f / (1.0f + potentialSize * DecayFactor);

        return multiplier < MinimumMultiplier ? MinimumMultiplier : multiplier;
    }
}
