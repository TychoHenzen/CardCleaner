using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using Godot;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
///     TileVisualLoader chooses the compiled atlas or the original tileset for the tile catalog. The loader is internal
///     and shares the Scripts assembly with these tests, so they call it directly.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileVisualLoaderTest
{
    [AfterTest]
    public static void TearDown()
    {
        CompiledAtlasLoader.ClearCache();
    }

    [TestCase]
    public static void CompiledAtlasAvailableUsesTheAtlasTileSizeAndKeepsEveryTileInOrder()
    {
        var result = TileDataLoader.LoadTileRegistry(new GodotAssetPaths());
        var atlasTileSize = CompiledAtlasLoader.LoadMapping()?.Atlas?.TileSize ?? 16;

        var catalog = TileVisualLoader.Load(result, null);

        AssertBool(catalog.UsingCompiledAtlas).IsTrue();
        AssertInt(catalog.TilesetConfig.BaseTileSize.X).IsEqual(atlasTileSize);
        AssertInt(catalog.TilesetConfig.BaseTileSize.Y).IsEqual(atlasTileSize);
        AssertInt(catalog.Tiles.Count).IsEqual(result.Tiles.Count);
        AssertBool(catalog.Tiles.Select(t => t.Id).SequenceEqual(result.Tiles.Select(t => t.Id))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MissingMappingFileKeepsTheOriginalTileset()
    {
        var result = TileDataLoader.LoadTileRegistry(new GodotAssetPaths());
        var mappingPath = $"user://tile-visual-missing-{Guid.NewGuid():N}.json";

        var catalog = TileVisualLoader.Load(result, mappingPath);

        AssertOriginalTileset(catalog, result);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MappingWhoseAtlasImageDoesNotLoadKeepsTheOriginalTileset()
    {
        var mappingPath = $"user://tile-visual-atlas-{Guid.NewGuid():N}.json";
        var absolutePath = ProjectSettings.GlobalizePath(mappingPath);
        try
        {
            var mapping = new AtlasMapping.AtlasMappingData
            {
                Version = "test",
                Atlas = new AtlasMapping.AtlasInfo
                {
                    Path = "user://missing-atlas.png", Width = 16, Height = 16, TileSize = 16
                },
                Sources = new Dictionary<string, Dictionary<string, AtlasMapping.TileAtlasRect>>()
            };
            File.WriteAllText(absolutePath, JsonSerializer.Serialize(mapping));
            var result = TileDataLoader.LoadTileRegistry(new GodotAssetPaths());

            var catalog = TileVisualLoader.Load(result, mappingPath);

            AssertOriginalTileset(catalog, result);
        }
        finally
        {
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
        }
    }

    private static void AssertOriginalTileset(TileVisualCatalog catalog, TileRegistryResult result)
    {
        AssertBool(catalog.UsingCompiledAtlas).IsFalse();
        AssertString(catalog.TilesetPath).IsEqual(result.TilesetPath);
        AssertThat(catalog.TilesetConfig).IsEqual(result.TilesetConfig);
        AssertBool(catalog.Tiles.SequenceEqual(result.Tiles)).IsTrue();
    }
}
