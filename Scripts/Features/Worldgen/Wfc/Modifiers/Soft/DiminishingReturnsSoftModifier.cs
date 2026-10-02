using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>
/// Applies diminishing returns to tile weights based on connected blob size.
/// Prevents single tile type from dominating the entire map.
///
/// Formula: multiplier = 1 / (1 + (size - MinimumBlobSize) * DecayFactor)
///
/// IMPORTANT: MinimumBlobSize should match target region size (e.g., 30-50 tiles).
/// Regions below this size grow freely; decay only applies to oversized regions.
///
/// With MinimumBlobSize=30 and DecayFactor=0.05:
/// - Size 30: 1.0x (no penalty - at target)
/// - Size 50: 1/(1 + 20*0.05) = 0.5x (moderate decay)
/// - Size 100: 1/(1 + 70*0.05) = 0.22x (strong decay)
/// </summary>
public class DiminishingReturnsSoftModifier : IWfcConstraint
{
    private readonly BlobSizeTracker _blobTracker;

    /// <summary>
    /// Decay factor controlling how quickly weights diminish for oversized regions.
    /// Lower values = gentler decay, allowing larger regions before strong penalty.
    /// With 0.05: Regions can grow to ~50 tiles before 50% penalty.
    /// </summary>
    public float DecayFactor { get; set; } = 0.05f;

    /// <summary>
    /// Target region size - regions below this grow freely (no penalty).
    /// Should match target region size (e.g., 30 for 30-100 tile target).
    /// </summary>
    public int MinimumBlobSize { get; set; } = 30;

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
        int potentialSize;

        // Use appropriate method based on topology type
        if (context.Topology is WfcGrid grid)
        {
            var position = grid.CellIdToPosition(context.CellId);
            potentialSize = _blobTracker.GetPotentialBlobSize(
                position,
                context.TileId,
                grid);
        }
        else
        {
            // Generic topology - use cell ID based method
            potentialSize = _blobTracker.GetPotentialBlobSize(
                context.CellId,
                context.TileId,
                context.Topology);
        }

        if (potentialSize <= MinimumBlobSize)
            return 1.0f;

        // Formula: 1 / (1 + (size - target) * decay)
        // Only penalize growth beyond target region size
        var excess = potentialSize - MinimumBlobSize;
        var multiplier = 1.0f / (1.0f + excess * DecayFactor);

        return multiplier < MinimumMultiplier ? MinimumMultiplier : multiplier;
    }
}
