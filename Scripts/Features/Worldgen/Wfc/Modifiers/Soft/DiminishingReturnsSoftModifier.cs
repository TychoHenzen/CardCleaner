using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>
/// Applies diminishing returns to tile weights based on connected blob size.
/// Counterbalances continuity bias to prevent single tile type domination.
///
/// Formula: multiplier = 1 / (1 + potentialBlobSize * DecayFactor)
///
/// With default DecayFactor=0.5:
/// - Blob size 1:   1 / (1 + 1 * 0.5)   = 0.667 (net with 5x continuity: 3.33x)
/// - Blob size 5:   1 / (1 + 5 * 0.5)   = 0.286 (net: 1.43x)
/// - Blob size 10:  1 / (1 + 10 * 0.5)  = 0.167 (net: 0.83x - now a penalty!)
/// - Blob size 20:  1 / (1 + 20 * 0.5)  = 0.091 (net: 0.45x)
///
/// Crossover point where continuity becomes penalty: ~8 tiles (5 * 0.2 = 1.0)
/// </summary>
public class DiminishingReturnsSoftModifier : IWfcConstraint
{
    private readonly BlobSizeTracker _blobTracker;

    /// <summary>
    /// Decay factor controlling how quickly weights diminish.
    /// Higher values = faster decay, smaller blobs.
    /// Default 0.5 targets ~8-10 tile blobs before continuity becomes a penalty.
    /// </summary>
    public float DecayFactor { get; set; } = 0.5f;

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
