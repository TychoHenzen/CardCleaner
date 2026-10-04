using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.AutoTileHelperScenarios;

/// <summary>
///     AutoTileHelperVariantLookupTest scenarios split out of AutoTileHelperTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileHelperVariantLookupTest : AutoTileHelperTestBase
{
    // ==================== GetAutoTileVariant ====================

    /// <summary>
    /// Test GetAutoTileVariant returns variant when available.
    /// </summary>
    [TestCase]
    public void TestGetAutoTileVariant_ReturnsVariantWhenAvailable()
    {
        var variants = CreateVariantArray(16);
        variants[5] = new Vector2I(10, 5);
        var tileDef = CreateTileDefinitionWithVariants("grass", "corner16", variants);

        // Setup: SE + SW neighbors = bitmask 6 (2 + 4)
        var pos = new Vector2I(5, 5);
        var grassPositions = new HashSet<Vector2I>
        {
            new(6, 6), // SE (value 2)
            new(4, 6)  // SW (value 4)
        };
        Func<Vector2I, string?> getTileId = p => grassPositions.Contains(p) ? "grass" : null;

        var variant = AutoTileHelper.GetAutoTileVariant(pos, tileDef, getTileId);

        // Bitmask 6 should return a variant (from format, not from AutoTileVariants array)
        // Note: GetAutoTileVariant delegates to tileDef.GetVariantDefinition which uses the format registry
        // For built-in formats, variants use placeholder coords (bitmask, 0)
        AssertThat(variant).IsNotNull();
    }

    /// <summary>
    /// Test GetAutoTileVariant returns null when tile has no variants.
    /// </summary>
    [TestCase]
    public void TestGetAutoTileVariant_ReturnsNullWhenNoVariants()
    {
        var tileDef = CreateTileDefinition("grass", "corner16", hasVariants: false);
        Func<Vector2I, string?> getTileId = _ => "grass";

        var variant = AutoTileHelper.GetAutoTileVariant(new Vector2I(5, 5), tileDef, getTileId);

        AssertThat(variant).IsNull();
    }

    // ==================== GetAutoTileCoords ====================

    /// <summary>
    /// Test GetAutoTileCoords returns base coords when no variants.
    /// </summary>
    [TestCase]
    public void TestGetAutoTileCoords_ReturnsBaseCoordsWhenNoVariants()
    {
        var tileDef = CreateTileDefinition("grass", "corner16", hasVariants: false);
        Func<Vector2I, string?> getTileId = _ => "grass";

        var coords = AutoTileHelper.GetAutoTileCoords(new Vector2I(5, 5), tileDef, getTileId);

        AssertThat(coords).IsEqual(tileDef.AtlasCoords);
    }

    /// <summary>
    /// Test GetAutoTileCoords returns variant coords when available.
    /// </summary>
    [TestCase]
    public void TestGetAutoTileCoords_ReturnsVariantCoords()
    {
        var variants = CreateVariantArray(16);
        variants[0] = new Vector2I(99, 99); // Bitmask 0 (isolated)
        var tileDef = CreateTileDefinitionWithVariants("grass", "corner16", variants);
        Func<Vector2I, string?> getTileId = _ => null; // No neighbors = bitmask 0

        var coords = AutoTileHelper.GetAutoTileCoords(new Vector2I(5, 5), tileDef, getTileId);

        AssertThat(coords).IsEqual(new Vector2I(99, 99));
    }

    private static TileDefinition CreateTileDefinitionWithVariants(
        string id,
        string formatName,
        Vector2I?[] variants)
    {
        return new TileDefinition(
            id,
            id,
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                AutoTileVariants = variants,
                AutoTileFormatName = formatName
            });
    }
}
