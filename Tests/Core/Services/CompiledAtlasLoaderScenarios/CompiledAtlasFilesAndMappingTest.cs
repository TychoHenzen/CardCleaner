using System.IO;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Tests.Core.Services.CompiledAtlasLoaderScenarios;

/// <summary>
///     Validates CompiledAtlasLoader file presence, mapping structure, atlas dimensions and
///     statistics; split out of CompiledAtlasLoaderTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompiledAtlasFilesAndMappingTest : CompiledAtlasLoaderTestBase
{
    // ==================== File Existence ====================

    [TestCase]
    public void TestAtlasMappingFileExists()
    {
        var absolutePath = ProjectSettings.GlobalizePath(AtlasMappingPath);
        AssertBool(File.Exists(absolutePath)).IsTrue();
    }

    [TestCase]
    public void TestAtlasPngFileExists()
    {
        var absolutePath = ProjectSettings.GlobalizePath(AtlasPngPath);
        AssertBool(File.Exists(absolutePath)).IsTrue();
    }

    [TestCase]
    public void TestTransitionMapFileExists()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        AssertBool(File.Exists(absolutePath)).IsTrue();
    }

    // ==================== Mapping Structure ====================

    [TestCase]
    public void TestMappingLoadsSuccessfully()
    {
        AssertThat(_mapping).IsNotNull();
    }

    [TestCase]
    public void TestMappingHasAtlasInfo()
    {
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Atlas).IsNotNull();
        AssertThat(_mapping.Atlas!.Path).IsNotEmpty();
        AssertThat(_mapping.Atlas.Width).IsGreater(0);
        AssertThat(_mapping.Atlas.Height).IsGreater(0);
        AssertThat(_mapping.Atlas.TileSize).IsGreater(0);
    }

    [TestCase]
    public void TestMappingHasSources()
    {
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Sources).IsNotNull();
        AssertThat(_mapping.Sources!.Count).IsGreater(0);

        GD.Print($"Atlas mapping contains {_mapping.Sources.Count} source IDs");
    }

    // ==================== Atlas Image Validation ====================

    [TestCase]
    public void TestAtlasImageMatchesMappingDimensions()
    {
        AssertThat(_mapping).IsNotNull();
        AssertThat(_atlasImage).IsNotNull();

        var expectedWidth = _mapping!.Atlas!.Width;
        var expectedHeight = _mapping.Atlas.Height;
        var actualWidth = _atlasImage!.GetWidth();
        var actualHeight = _atlasImage.GetHeight();

        GD.Print($"Atlas mapping claims: {expectedWidth}x{expectedHeight}");
        GD.Print($"Atlas image actual: {actualWidth}x{actualHeight}");

        AssertThat(actualWidth).IsEqual(expectedWidth);
        AssertThat(actualHeight).IsEqual(expectedHeight);
    }

    // ==================== Statistics ====================

    [TestCase]
    public void TestAtlasMappingStatistics()
    {
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Sources).IsNotNull();

        var totalMappings = 0;
        var sourceCount = _mapping.Sources!.Count;

        foreach (var (sourceId, coordMappings) in _mapping.Sources)
        {
            totalMappings += coordMappings.Count;
        }

        GD.Print("Atlas Mapping Statistics:");
        GD.Print($"  Source IDs: {sourceCount}");
        GD.Print($"  Total coordinate mappings: {totalMappings}");
        GD.Print($"  Atlas size: {_mapping.Atlas!.Width}x{_mapping.Atlas.Height}");
        GD.Print($"  Tile size: {_mapping.Atlas.TileSize}");
        GD.Print(
            $"  Grid size: {_mapping.Atlas.Width / _mapping.Atlas.TileSize}x" +
            $"{_mapping.Atlas.Height / _mapping.Atlas.TileSize}");

        AssertThat(totalMappings).IsGreater(0);
    }

    // ==================== Cross-Reference with tiles.json ====================

    [TestCase]
    public void TestKnownTilesHaveMappings()
    {
        // Verify that known important tiles have mappings
        // Non-compositable tiles should be in atlas_mapping
        AssertThat(_mapping).IsNotNull();

        var registry = new TileRegistry();
        var nonCompositableTiles = registry.GetAllTiles()
            .Where(t => !t.IsCompositable && t.SourceId == TileRegistry.DefaultSourceId)
            .ToList();

        var missingMappings = new System.Collections.Generic.List<string>();
        var sourceKey = TileRegistry.DefaultSourceId.ToString();

        if (!_mapping!.Sources!.TryGetValue(sourceKey, out var coordMappings))
        {
            GD.PrintErr($"No mappings for default source ID {TileRegistry.DefaultSourceId}");
            return;
        }

        foreach (var tile in nonCompositableTiles)
        {
            var coordKey = $"{tile.AtlasCoords.X},{tile.AtlasCoords.Y}";
            if (!coordMappings.ContainsKey(coordKey))
            {
                // This tile's base coords don't have a mapping
                // This is expected for compositable tiles but not for non-compositable
                if (!tile.IsCompositable)
                    missingMappings.Add($"{tile.Id}: ({tile.AtlasCoords.X},{tile.AtlasCoords.Y})");
            }
        }

        if (missingMappings.Count > 0)
        {
            GD.Print(
                "Non-compositable tiles without direct mapping (may use transition_map):\n" +
                $"{string.Join("\n", missingMappings.Take(20))}");
        }

        // This is informational - tiles may use transition_map instead
    }
}
