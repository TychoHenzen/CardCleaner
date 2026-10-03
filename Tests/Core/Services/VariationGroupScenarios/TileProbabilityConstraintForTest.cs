using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;


namespace CardCleaner.Tests.Core.Services.VariationGroupScenarios;

/// <summary>
/// Test-friendly version of TileProbabilityConstraint that works with ITileRegistry.
/// </summary>
internal class TileProbabilityConstraintForTest : IWfcConstraint
{
    private readonly ITileRegistry _tileRegistry;
    private Dictionary<string, string>? _selectedVariants;
    public float MinModifier { get; set; } = 0.01f;

    public TileProbabilityConstraintForTest(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    public void SetSelectedVariants(Dictionary<string, string>? selectedVariants)
    {
        _selectedVariants = selectedVariants;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        var tile = _tileRegistry.GetTile(context.TileId);
        if (tile == null)
            return 1f;

        var group = _tileRegistry.GetVariationGroup(context.TileId);

        if (group != null)
        {
            var density = group.MaxWeight;

            if (group.Mode == VariationMode.PerGeneration)
            {
                if (_selectedVariants != null &&
                    _selectedVariants.TryGetValue(group.BaseName, out var selectedTileId))
                {
                    if (context.TileId != selectedTileId)
                    {
                        return 0.0f; // Hard exclusion
                    }
                }
                return System.Math.Max(MinModifier, density);
            }

            var normalizedWeight = group.GetNormalizedWeight(context.TileId);
            return System.Math.Max(MinModifier, density * normalizedWeight);
        }

        return System.Math.Max(MinModifier, tile.Probability);
    }
}
