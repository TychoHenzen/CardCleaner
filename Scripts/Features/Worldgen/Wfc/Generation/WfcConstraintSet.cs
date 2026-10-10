using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// The base constraints a map generator registers on its tile selector, in registration order.
/// Registry-backed constraints only exist when a tile registry was supplied.
/// </summary>
internal sealed class WfcConstraintSet
{
    private readonly DiminishingReturnsSoftModifier _diminishingReturns;
    private readonly SpatialCoherenceConstraint _spatialCoherence;
    private readonly CompactnessSoftModifier _compactness;
    private readonly AutoTileGapConstraint? _autoTileGap;
    private readonly NoSolidFillConstraint? _noSolidFill;
    private readonly BitmaskValidityConstraint? _bitmaskValidity;
    private readonly TileProbabilityConstraint? _tileProbability;

    internal WfcConstraintSet(
        DiminishingReturnsSoftModifier diminishingReturns,
        SpatialCoherenceConstraint spatialCoherence,
        CompactnessSoftModifier compactness,
        ITileRegistry? tileRegistry)
    {
        _diminishingReturns = diminishingReturns;
        _spatialCoherence = spatialCoherence;
        _compactness = compactness;
        _autoTileGap = tileRegistry != null ? new AutoTileGapConstraint(tileRegistry) : null;
        _noSolidFill = tileRegistry != null ? new NoSolidFillConstraint(tileRegistry) : null;
        _bitmaskValidity = tileRegistry != null ? new BitmaskValidityConstraint(tileRegistry) : null;
        _tileProbability = tileRegistry != null ? new TileProbabilityConstraint(tileRegistry) : null;
    }

    internal void SetSelectedVariants(Dictionary<string, string>? selectedVariants)
    {
        _tileProbability?.SetSelectedVariants(selectedVariants);
    }

    /// <summary>
    /// Replaces the selector's constraints with the enabled base constraints.
    /// </summary>
    internal void ApplyTo(WfcTileSelector selector, WfcConstraintToggles toggles)
    {
        selector.ClearConstraints();

        if (toggles.DiminishingReturns)
            selector.AddConstraint(_diminishingReturns);

        if (toggles.SpatialCoherence)
            selector.AddConstraint(_spatialCoherence);

        // Encourage compact blob shapes (boosts corner/gap fills)
        if (toggles.Compactness)
            selector.AddConstraint(_compactness);

        // Enforce 1-tile gap between different auto-tile types (8-neighbor check)
        if (_autoTileGap != null)
            selector.AddConstraint(_autoTileGap);

        // Prevent 2x2 solid regions for tilesets lacking bitmask 15 (solid fill)
        if (_noSolidFill != null)
            selector.AddConstraint(_noSolidFill);

        // Prevent tile configurations that would create disallowed bitmask patterns
        if (_bitmaskValidity != null)
            selector.AddConstraint(_bitmaskValidity);

        // Apply tile probability/density from TSX and variation groups
        if (_tileProbability != null)
            selector.AddConstraint(_tileProbability);
    }
}
