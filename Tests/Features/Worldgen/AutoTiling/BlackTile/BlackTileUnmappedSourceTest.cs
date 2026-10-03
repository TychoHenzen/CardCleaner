using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BlackTile;

/// <summary>
///     BlackTileUnmappedSourceTest scenarios split out of BlackTileReproductionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BlackTileUnmappedSourceTest : BlackTileReproductionTestBase
{
    // ==================== Unmapped Source ID Detection ====================

    [TestCase]
    public void TestIdentifyTilesWithUnmappedSourceIds()
    {
        AssertThat(_atlasMapping).IsNotNull();

        var unmappedTiles = new List<string>();
        var mappedSourceIds = _atlasMapping!.Sources!.Keys.Select(int.Parse).ToHashSet();

        foreach (var tile in _registry.GetAllTiles())
        {
            // Non-compositable tiles should have their base coords in atlas_mapping
            if (!tile.IsCompositable && !mappedSourceIds.Contains(tile.SourceId))
            {
                unmappedTiles.Add($"{tile.Id}: sourceId={tile.SourceId} not in atlas_mapping");
            }
        }

        if (unmappedTiles.Count > 0)
        {
            GD.PrintErr($"BLACK TILE CAUSE: Tiles with unmapped source IDs:\n{string.Join("\n", unmappedTiles)}");
        }

        // Log count for diagnosis
        GD.Print($"Tiles with unmapped source IDs: {unmappedTiles.Count}");
    }

    [TestCase]
    public void TestIdentifyTilesWithUnmappedCoordinates()
    {
        AssertThat(_atlasMapping).IsNotNull();

        var unmappedCoords = new List<string>();

        foreach (var tile in _registry.GetAllTiles())
        {
            if (tile.IsCompositable)
                continue; // Compositable tiles use transition_map

            var sourceKey = tile.SourceId.ToString();
            var coordKey = $"{tile.AtlasCoords.X},{tile.AtlasCoords.Y}";

            if (!_atlasMapping!.Sources!.TryGetValue(sourceKey, out var coordMappings) ||
                !coordMappings.ContainsKey(coordKey))
            {
                // Check if using compiled atlas sourceId (coords already translated)
                if (tile.SourceId != CompiledAtlasSourceId)
                {
                    unmappedCoords.Add(
                        $"{tile.Id}: ({tile.AtlasCoords.X},{tile.AtlasCoords.Y}) " +
                        $"from source {tile.SourceId}");
                }
            }
        }

        if (unmappedCoords.Count > 0)
        {
            GD.PrintErr(
                "BLACK TILE CAUSE: Tiles with unmapped coordinates:\n" +
                $"{string.Join("\n", unmappedCoords.Take(20))}...");
        }

        GD.Print($"Tiles with unmapped coordinates: {unmappedCoords.Count}");
    }

    // ==================== Missing Transition Detection ====================

    [TestCase]
    public void TestIdentifyCompositableTilesWithoutTransitions()
    {
        AssertThat(_transitionMap).IsNotNull();

        var missingTransitions = new List<string>();
        var allBorderIds = _transitionMap!.GetAllBorderIds().ToHashSet();

        foreach (var tile in _registry.GetAllTiles())
        {
            if (!tile.IsCompositable)
                continue;

            // Check if this tile has ANY transition entry
            if (!allBorderIds.Contains(tile.Id) && !allBorderIds.Contains($"{tile.Id}_border"))
            {
                missingTransitions.Add(tile.Id);
            }
        }

        if (missingTransitions.Count > 0)
        {
            GD.PrintErr(
                "BLACK TILE CAUSE: Compositable tiles without transitions:\n" +
                $"{string.Join(", ", missingTransitions)}");
        }

        AssertThat(missingTransitions.Count).IsEqual(0);
    }
}
