using System.Linq;
using CardCleaner.Scripts.Core.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class TileCatalogLoadTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void LoadReturnsARegistryWithTheSameTilesAsTheConstructor()
    {
        var loaded = TileCatalogLoad.Load(new GodotAssetPaths());
        var direct = new TileRegistry(new GodotAssetPaths());

        var loadedIds = loaded.GetAllTiles().Select(tile => tile.Id).ToList();
        var directIds = direct.GetAllTiles().Select(tile => tile.Id).ToList();

        AssertBool(loadedIds.Count > 0).IsTrue();
        AssertBool(loadedIds.SequenceEqual(directIds)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LoadBuildsANewRegistryOnEveryCall()
    {
        var first = TileCatalogLoad.Load(new GodotAssetPaths());
        var second = TileCatalogLoad.Load(new GodotAssetPaths());

        AssertBool(ReferenceEquals(first, second)).IsFalse();
    }
}
