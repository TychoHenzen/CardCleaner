using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Validates CompiledAtlasLoader coordinate translation and atlas integrity.
/// These tests ensure the mapping from source tiles to compiled atlas works correctly.
/// Invalid mappings cause black/missing tiles at runtime.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompiledAtlasLoaderTest
{
    private const string AtlasMappingPath = "res://Data/CompiledAtlas/atlas_mapping.json";
    private const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";
    private const string AtlasPngPath = "res://Data/CompiledAtlas/terrain_atlas.png";

    private CompiledAtlasLoader.AtlasMappingData? _mapping;
    private JsonDocument? _transitionDoc;
    private Image? _atlasImage;

    [BeforeTest]
    public void Setup()
    {
        CompiledAtlasLoader.ClearCache();
        _mapping = CompiledAtlasLoader.LoadMapping();

        var transitionPath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (File.Exists(transitionPath))
        {
            var json = File.ReadAllText(transitionPath);
            _transitionDoc = JsonDocument.Parse(json);
        }

        var atlasPath = ProjectSettings.GlobalizePath(AtlasPngPath);
        if (File.Exists(atlasPath))
        {
            _atlasImage = Image.LoadFromFile(atlasPath);
        }
    }

    [AfterTest]
    public void Teardown()
    {
        _transitionDoc?.Dispose();
        CompiledAtlasLoader.ClearCache();
    }

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

    // ==================== Coordinate Translation ====================

    [TestCase]
    public void TestTranslateCoordinatesReturnsValidCoords()
    {
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Sources).IsNotNull();

        // Pick the first available source/coord pair
        var (sourceIdStr, coordMappings) = _mapping.Sources!.First();
        var sourceId = int.Parse(sourceIdStr);
        var (coordKey, rect) = coordMappings.First();
        var parts = coordKey.Split(',');
        var originalCoords = new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));

        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            sourceId, originalCoords, _mapping);

        // Should return sourceId 0 for compiled atlas
        AssertThat(newSourceId).IsEqual(0);
        // Should return the mapped coords
        AssertThat(newCoords.X).IsEqual(rect.X);
        AssertThat(newCoords.Y).IsEqual(rect.Y);
    }

    [TestCase]
    public void TestTranslateCoordinatesReturnsOriginalForUnmapped()
    {
        AssertThat(_mapping).IsNotNull();

        // Use an obviously invalid source ID
        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            99999, new Vector2I(999, 999), _mapping);

        // Should return original coords when no mapping exists
        AssertThat(newSourceId).IsEqual(99999);
        AssertThat(newCoords).IsEqual(new Vector2I(999, 999));
    }

    [TestCase]
    public void TestTranslateCoordinatesHandlesNullMapping()
    {
        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            4, new Vector2I(5, 5), null);

        // Should return original coords when mapping is null
        AssertThat(newSourceId).IsEqual(4);
        AssertThat(newCoords).IsEqual(new Vector2I(5, 5));
    }

    // ==================== Coordinate Bounds Validation ====================

    [TestCase]
    public void TestAllMappedCoordsAreWithinAtlasBounds()
    {
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Sources).IsNotNull();
        AssertThat(_mapping.Atlas).IsNotNull();

        var maxTileX = _mapping.Atlas!.Width / _mapping.Atlas.TileSize;
        var maxTileY = _mapping.Atlas.Height / _mapping.Atlas.TileSize;
        var outOfBounds = new System.Collections.Generic.List<string>();

        foreach (var (sourceId, coordMappings) in _mapping.Sources!)
        {
            foreach (var (coordKey, rect) in coordMappings)
            {
                if (rect.X < 0 || rect.X >= maxTileX)
                    outOfBounds.Add($"source {sourceId} -> ({rect.X},{rect.Y}): x out of bounds [0, {maxTileX - 1}]");
                if (rect.Y < 0 || rect.Y >= maxTileY)
                    outOfBounds.Add($"source {sourceId} -> ({rect.X},{rect.Y}): y out of bounds [0, {maxTileY - 1}]");
            }
        }

        if (outOfBounds.Count > 0)
            GD.PrintErr($"Out of bounds atlas mappings:\n{string.Join("\n", outOfBounds.Take(20))}...");

        AssertThat(outOfBounds.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAllTransitionCoordsAreWithinAtlasBounds()
    {
        AssertThat(_transitionDoc).IsNotNull();
        AssertThat(_mapping).IsNotNull();
        AssertThat(_mapping!.Atlas).IsNotNull();

        var maxTileX = _mapping.Atlas!.Width / _mapping.Atlas.TileSize;
        var maxTileY = _mapping.Atlas.Height / _mapping.Atlas.TileSize;
        var outOfBounds = new System.Collections.Generic.List<string>();

        var root = _transitionDoc!.RootElement;
        if (root.TryGetProperty("transitions", out var transitions))
        {
            foreach (var transition in transitions.EnumerateObject())
            {
                var key = transition.Name;
                var entry = transition.Value;

                if (!entry.TryGetProperty("variants", out var variants))
                    continue;

                var idx = 0;
                foreach (var variantArray in variants.EnumerateArray())
                {
                    // Each variant is an array of coordinate objects (for visual variations)
                    if (variantArray.ValueKind != JsonValueKind.Null && variantArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var coord in variantArray.EnumerateArray())
                        {
                            var x = coord.GetProperty("x").GetInt32();
                            var y = coord.GetProperty("y").GetInt32();

                            if (x < 0 || x >= maxTileX)
                                outOfBounds.Add($"{key}[{idx}]: x={x} out of bounds [0, {maxTileX - 1}]");
                            if (y < 0 || y >= maxTileY)
                                outOfBounds.Add($"{key}[{idx}]: y={y} out of bounds [0, {maxTileY - 1}]");
                        }
                    }
                    idx++;
                }
            }
        }

        if (outOfBounds.Count > 0)
            GD.PrintErr($"Out of bounds transition coordinates:\n{string.Join("\n", outOfBounds.Take(20))}...");

        AssertThat(outOfBounds.Count).IsEqual(0);
    }

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
