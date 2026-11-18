using System.IO;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
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

    // ==================== Transition Tiles Created When Painted ====================

    [TestCase]
    public void EveryInBoundsTransitionCoordinateIsATileOrCreatedWhenPainted()
    {
        var tiles = DefaultAtlasTiles();
        var atlasSource = (TileSetAtlasSource)tiles.TileSet.GetSource(CompiledAtlasTiles.SourceId);
        var columns = atlasSource.Texture!.GetWidth() / atlasSource.TextureRegionSize.X;
        var rows = atlasSource.Texture.GetHeight() / atlasSource.TextureRegionSize.Y;

        var inBounds = TransitionCoordinates()
            .Where(coords => coords.X < columns && coords.Y < rows)
            .ToList();
        var unknown = inBounds.Where(coords => !tiles.Knows(coords)).ToList();

        // ASSUMPTION: the game's transition map names thousands of tiles, so a near-empty list means it was not read.
        AssertThat(inBounds.Count).IsGreater(1000);
        AssertThat(string.Join(", ", unknown.Take(20))).IsEmpty();
    }

    [TestCase]
    public void PaintingADeferredTransitionTileCreatesIt()
    {
        var tiles = DefaultAtlasTiles();
        var atlasSource = (TileSetAtlasSource)tiles.TileSet.GetSource(CompiledAtlasTiles.SourceId);
        var deferred = TransitionCoordinates().First(coords => tiles.Knows(coords) && !atlasSource.HasTile(coords));
        var layer = new TileMapLayer { TileSet = tiles.TileSet };
        try
        {
            AtlasCells.Set(layer, new Vector2I(2, 3), CompiledAtlasTiles.SourceId, deferred);

            AssertThat(atlasSource.HasTile(deferred)).IsTrue();
            AssertThat(layer.GetCellTileData(new Vector2I(2, 3))).IsNotNull();
            AssertThat(layer.GetCellAtlasCoords(new Vector2I(2, 3))).IsEqual(deferred);
        }
        finally
        {
            layer.Free();
        }
    }

    [TestCase]
    public void PaintingACoordinateOutsideTheAtlasCreatesNoTile()
    {
        var tiles = DefaultAtlasTiles();
        var atlasSource = (TileSetAtlasSource)tiles.TileSet.GetSource(CompiledAtlasTiles.SourceId);
        var outside = new Vector2I(100000, 100000);
        var layer = new TileMapLayer { TileSet = tiles.TileSet };
        try
        {
            AtlasCells.Set(layer, Vector2I.Zero, CompiledAtlasTiles.SourceId, outside);

            AssertThat(tiles.Knows(outside)).IsFalse();
            AssertThat(atlasSource.HasTile(outside)).IsFalse();
        }
        finally
        {
            layer.Free();
        }
    }

    private static CompiledAtlasTiles DefaultAtlasTiles()
    {
        var tileSet = CompiledAtlasLoader.LoadCompiledTileSet();
        AssertThat(tileSet).IsNotNull();
        var tiles = CompiledAtlasLoader.TilesFor(tileSet!);
        // A bool, because gdUnit's AssertThat cannot bind an internal type.
        AssertThat(tiles != null).IsTrue();
        return tiles!;
    }

    private System.Collections.Generic.IEnumerable<Vector2I> TransitionCoordinates()
    {
        AssertThat(_transitionDoc).IsNotNull();
        foreach (var transition in _transitionDoc!.RootElement.GetProperty("transitions").EnumerateObject())
        {
            if (!transition.Value.TryGetProperty("variants", out var variants)
                || variants.ValueKind != System.Text.Json.JsonValueKind.Array)
                continue;

            foreach (var options in variants.EnumerateArray())
            {
                if (options.ValueKind != System.Text.Json.JsonValueKind.Array)
                    continue;

                foreach (var coords in options.EnumerateArray())
                {
                    var atlasCoords = new Vector2I(coords.GetProperty("x").GetInt32(), coords.GetProperty("y").GetInt32());
                    if (atlasCoords.X >= 0 && atlasCoords.Y >= 0)
                        yield return atlasCoords;
                }
            }
        }
    }

    // ==================== Cache Behavior ====================

    [TestCase]
    public void TestMappingIsReadFromDiskOnEveryLoad()
    {
        var mappingPath = CreateTemporaryPath(".json");
        WriteMapping(mappingPath, "user://first-atlas.png");
        AssertString(CompiledAtlasLoader.LoadMapping(mappingPath)!.Atlas!.Path).IsEqual("user://first-atlas.png");

        WriteMapping(mappingPath, "user://second-atlas.png");

        AssertString(CompiledAtlasLoader.LoadMapping(mappingPath)!.Atlas!.Path).IsEqual("user://second-atlas.png");
    }

    [TestCase]
    public void TestATileSetForAnotherMappingLeavesTheDefaultOneCached()
    {
        var defaultTileSet = CompiledAtlasLoader.LoadCompiledTileSet();
        var mappingPath = CreateTemporaryPath(".json");
        var atlasPath = CreateTemporaryPath(".png");
        WriteMapping(mappingPath, atlasPath);
        var atlasImage = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        AssertThat(atlasImage.SavePng(ProjectSettings.GlobalizePath(atlasPath))).IsEqual(Error.Ok);

        var otherTileSet = CompiledAtlasLoader.LoadCompiledTileSet(mappingPath);

        AssertThat(otherTileSet).IsNotNull();
        AssertThat(otherTileSet).IsNotSame(defaultTileSet);
        AssertThat(CompiledAtlasLoader.LoadCompiledTileSet()).IsSame(defaultTileSet);
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
        WriteMapping(mappingPath, atlasPath);

        AssertThat(CompiledAtlasLoader.LoadCompiledTileSet(mappingPath)).IsNull();

        var atlasImage = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        AssertThat(atlasImage.SavePng(ProjectSettings.GlobalizePath(atlasPath))).IsEqual(Error.Ok);

        var tileSet = CompiledAtlasLoader.LoadCompiledTileSet(mappingPath);
        AssertThat(tileSet).IsNotNull();
        AssertThat(tileSet!.GetSourceCount()).IsGreater(0);
    }
}
