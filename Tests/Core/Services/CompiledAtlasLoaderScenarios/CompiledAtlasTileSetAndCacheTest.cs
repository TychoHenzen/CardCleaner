using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Tests.Core.Services.CompiledAtlasLoaderScenarios;

/// <summary>
///     Validates CompiledAtlasLoader TileSet creation and cache retry behavior; split out of CompiledAtlasLoaderTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompiledAtlasTileSetAndCacheTest : CompiledAtlasLoaderTestBase
{
    // ==================== TileSet Creation ====================

    [TestCase]
    public void TestLoadCompiledTileSetSucceeds()
    {
        var tileSet = CompiledAtlasLoader.LoadCompiledTileSet();

        AssertThat(tileSet).IsNotNull();
        AssertThat(tileSet!.GetSourceCount()).IsGreater(0);

        var source0 = tileSet.GetSource(0);
        AssertThat(source0).IsNotNull();

        var atlasSource = source0 as TileSetAtlasSource;
        AssertThat(atlasSource).IsNotNull();
        AssertThat(atlasSource!.Texture).IsNotNull();

        GD.Print($"TileSet has {tileSet.GetSourceCount()} source(s)");
        GD.Print($"Texture size: {atlasSource.Texture!.GetSize()}");
    }

    [TestCase]
    public void TestTileSetHasTilesForAllMappedCoords()
    {
        var tileSet = CompiledAtlasLoader.LoadCompiledTileSet();
        AssertThat(tileSet).IsNotNull();

        var atlasSource = tileSet!.GetSource(0) as TileSetAtlasSource;
        AssertThat(atlasSource).IsNotNull();

        var missingTiles = new System.Collections.Generic.List<string>();

        if (_mapping?.Sources != null)
        {
            foreach (var (sourceId, coordMappings) in _mapping.Sources)
            {
                foreach (var (coordKey, rect) in coordMappings)
                {
                    var atlasCoords = new Vector2I(rect.X, rect.Y);
                    if (!atlasSource!.HasTile(atlasCoords))
                        missingTiles.Add($"source {sourceId} {coordKey} -> ({rect.X},{rect.Y})");
                }
            }
        }

        if (missingTiles.Count > 0)
            GD.PrintErr($"Missing tiles in TileSet:\n{string.Join("\n", missingTiles.Take(20))}...");

        AssertThat(missingTiles.Count).IsEqual(0);
    }

    // ==================== Cache Behavior ====================

    [TestCase]
    public void TestClearCacheResetsMappingState()
    {
        // First load to populate cache
        var mapping1 = CompiledAtlasLoader.LoadMapping();
        AssertThat(mapping1).IsNotNull();

        // Clear cache
        CompiledAtlasLoader.ClearCache();

        // Load again - should reload from disk
        var mapping2 = CompiledAtlasLoader.LoadMapping();
        AssertThat(mapping2).IsNotNull();

        // Both should be valid (not testing reference equality since cache may differ)
        AssertThat(mapping1!.Sources!.Count).IsEqual(mapping2!.Sources!.Count);
    }

    [TestCase]
    public void TestMissingMappingCanBeRetriedWithoutStaleCache()
    {
        var mappingPath = CreateTemporaryPath(".json");

        AssertThat(CompiledAtlasLoader.LoadMapping(mappingPath)).IsNull();

        WriteMapping(mappingPath, "user://missing-atlas.png");

        var mapping = CompiledAtlasLoader.LoadMapping(mappingPath);
        AssertThat(mapping).IsNotNull();
        AssertThat(mapping!.Atlas).IsNotNull();
    }

    [TestCase]
    public void TestMappingWithoutAtlasRootCanBeRetriedWithoutStaleCache()
    {
        var mappingPath = CreateTemporaryPath(".json");
        File.WriteAllText(ProjectSettings.GlobalizePath(mappingPath), "{\"version\":\"test\"}");

        AssertThat(CompiledAtlasLoader.LoadMapping(mappingPath)).IsNull();

        WriteMapping(mappingPath, "user://missing-atlas.png");

        var mapping = CompiledAtlasLoader.LoadMapping(mappingPath);
        AssertThat(mapping).IsNotNull();
        AssertThat(mapping!.Atlas).IsNotNull();
    }

    [TestCase]
    public void TestMalformedMappingCanBeRetriedWithoutStaleCache()
    {
        var mappingPath = CreateTemporaryPath(".json");
        File.WriteAllText(ProjectSettings.GlobalizePath(mappingPath), "not-json");

        AssertThat(CompiledAtlasLoader.LoadMapping(mappingPath)).IsNull();

        WriteMapping(mappingPath, "user://missing-atlas.png");

        var mapping = CompiledAtlasLoader.LoadMapping(mappingPath);
        AssertThat(mapping).IsNotNull();
        AssertThat(mapping!.Atlas).IsNotNull();
    }

    [TestCase]
    public void TestMissingAtlasCanBeRetriedAfterAtlasAppears()
    {
        var mappingPath = CreateTemporaryPath(".json");
        var atlasPath = CreateTemporaryPath(".png");
        var sourceMapping = JsonSerializer.Deserialize<CompiledAtlasLoader.AtlasMappingData>(
            File.ReadAllText(ProjectSettings.GlobalizePath(AtlasMappingPath)))!;
        sourceMapping.Atlas!.Path = atlasPath;
        File.WriteAllText(
            ProjectSettings.GlobalizePath(mappingPath),
            JsonSerializer.Serialize(sourceMapping));

        AssertThat(CompiledAtlasLoader.LoadCompiledTileSet(mappingPath)).IsNull();

        File.Copy(
            ProjectSettings.GlobalizePath(AtlasPngPath),
            ProjectSettings.GlobalizePath(atlasPath),
            overwrite: true);

        var tileSet = CompiledAtlasLoader.LoadCompiledTileSet(mappingPath);
        AssertThat(tileSet).IsNotNull();
        AssertThat(tileSet!.GetSourceCount()).IsGreater(0);
    }
}
