using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Applies tile probability as a weight modifier during WFC selection.
/// Uses TileDefinition.Probability for individual tiles and VariationGroup.MaxWeight for grouped tiles.
/// This enables the TSX probability attribute to control tile density.
///
/// For PerGeneration variation groups (e.g., grass1/grass2/grass3), this constraint
/// EXCLUDES non-selected variants by returning 0.0f, ensuring only one variant
/// from each group appears on the entire map.
/// </summary>
public class TileProbabilityConstraint : IWfcConstraint
{
    private readonly TileRegistry _tileRegistry;

    // Maps group base name → selected tile ID for this generation
    private Dictionary<string, string>? _selectedVariants;

    /// <summary>Sets the minimum probability modifier for eligible tiles.</summary>
    public float MinModifier { get; set; } = 0.01f;

    public TileProbabilityConstraint(TileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    /// <summary>
    /// Sets the selected variants for PerGeneration groups.
    /// Must be called before WFC generation starts.
    /// </summary>
    /// <param name="selectedVariants">Mapping of group base name to selected tile ID.</param>
    public void SetSelectedVariants(Dictionary<string, string>? selectedVariants)
    {
        _selectedVariants = selectedVariants;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        var tile = _tileRegistry.GetTile(context.TileId);
        if (tile == null)
            return 1f;

        // Check if tile is in a variation group
        var group = _tileRegistry.GetVariationGroup(context.TileId);

        if (group != null)
        {
            // For grouped tiles, use group's MaxWeight as base density
            // This ensures all variants in a group share the same density
            var density = group.MaxWeight;

            // For PerGeneration groups, EXCLUDE non-selected variants
            // Only the pre-selected variant should appear on this map
            if (group.Mode == VariationMode.PerGeneration)
            {
                // Check if we have a selected variant for this group
                if (_selectedVariants != null &&
                    _selectedVariants.TryGetValue(group.BaseName, out var selectedTileId))
                {
                    // If this tile is NOT the selected variant, completely exclude it
                    if (context.TileId != selectedTileId)
                    {
                        return 0.0f; // Hard exclusion - this variant cannot be placed
                    }
                    // This IS the selected variant - use full group density
                }
                // No selection made for this group - all variants allowed (fallback behavior)
                return System.Math.Max(MinModifier, density);
            }

            // For PerInstance groups, use individual tile's weight within the group
            // This maintains the ratio between variants (poppy weight 2 vs rose weight 1)
            var normalizedWeight = group.GetNormalizedWeight(context.TileId);
            return System.Math.Max(MinModifier, density * normalizedWeight);
        }

        // For non-grouped tiles, use direct probability
        return System.Math.Max(MinModifier, tile.Probability);
    }
}
