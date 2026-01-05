using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>
/// Applies diminishing returns to tile weights based on connected blob size.
/// Counterbalances continuity bias to prevent single tile type domination.
///
/// Formula: multiplier = 1 / (1 + potentialBlobSize * DecayFactor)
///
/// With DecayFactor=0.05 (balanced with SpatialCoherenceConstraint):
/// - Blob size 10:  1 / (1 + 10 * 0.05) = 0.667 (allows coherent growth)
/// - Blob size 40:  1 / (1 + 40 * 0.05) = 0.333 (target size, still growing)
/// - Blob size 80:  1 / (1 + 80 * 0.05) = 0.200 (decay begins)
/// - Blob size 100: 1 / (1 + 100 * 0.05) = 0.167 (prevents domination)
///
/// With SpatialCoherence BoostFactor=5.0, net effect at size 40: 6.0x * 0.333 = 2.0x boost
/// </summary>
public class DiminishingReturnsSoftModifier : IWfcConstraint
{
    private readonly BlobSizeTracker _blobTracker;

    /// <summary>
    /// Decay factor controlling how quickly weights diminish.
    /// Higher values = faster decay, smaller blobs.
    /// Reduced from 0.5 to 0.05 to work with SpatialCoherenceConstraint (target: 40 tiles).
    /// With 0.05: Decay becomes significant at 60-80 tiles, preventing single-type domination
    /// while allowing spatial coherence to form 40-tile regions.
    /// </summary>
    public float DecayFactor { get; set; } = 0.05f;

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
