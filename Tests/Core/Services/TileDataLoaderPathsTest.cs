using System.IO;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Tests that the tile loaders resolve res:// paths through the injected IAssetPaths: a fake project root with
/// no data, or with malformed data, gives a recoverable result instead of an exception.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileDataLoaderPathsTest
{
    private const string TilesJsonPath = "res://Data/Tiles/tiles.json";
    private const string TiledMapPath = "res://Data/Tiled/tileset.tmx";

    [TestCase]
    [TestCategory("Unit")]
    public static void TestEmptyRootGivesEmptyRegistryResult()
    {
        var root = TempAssetPaths.CreateTempRoot();
        try
        {
            var result = TileDataLoader.LoadTileRegistry(new TempAssetPaths(root), TilesJsonPath);

            AssertThat(result.Tiles).IsEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMalformedJsonGivesFallbackResult()
    {
        var root = TempAssetPaths.CreateTempRoot();
        try
        {
            var dataDir = Path.Combine(root, "Data", "Tiles");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "tiles.json"), "{ not json");

            var result = TileDataLoader.LoadTileRegistry(new TempAssetPaths(root), TilesJsonPath);

            AssertThat(result.Tiles).IsEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMissingTmxGivesNullMap()
    {
        var root = TempAssetPaths.CreateTempRoot();
        try
        {
            var map = TileDataLoader.LoadTmxMap(new TempAssetPaths(root), TiledMapPath);

            AssertThat(map).IsNull();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMalformedTmxGivesNullMap()
    {
        var root = TempAssetPaths.CreateTempRoot();
        try
        {
            var tmxPath = new TempAssetPaths(root).Globalize(TiledMapPath);
            Directory.CreateDirectory(Path.GetDirectoryName(tmxPath)!);
            File.WriteAllText(tmxPath, "this is not xml");

            var map = TileDataLoader.LoadTmxMap(new TempAssetPaths(root), TiledMapPath);

            AssertThat(map).IsNull();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
