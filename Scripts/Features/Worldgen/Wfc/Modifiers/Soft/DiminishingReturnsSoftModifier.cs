using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>Applies diminishing returns as connected blob size exceeds the target.</summary>
public class DiminishingReturnsSoftModifier : IWfcConstraint
{
    private readonly BlobSizeTracker _blobTracker;

    /// <summary>Controls decay strength for oversized regions.</summary>
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
