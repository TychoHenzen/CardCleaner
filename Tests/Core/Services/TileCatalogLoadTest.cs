using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.CompiledAtlas;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class TileCatalogLoadTest
{
    [TestCase]
    public static void LoadReturnsTheSameTilesOnEveryCall()
    {
        var loaded = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
        var direct = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());

        var loadedIds = loaded.GetAllTiles().Select(tile => tile.Id).ToList();
        var directIds = direct.GetAllTiles().Select(tile => tile.Id).ToList();

        AssertBool(loadedIds.Count > 0).IsTrue();
        AssertBool(loadedIds.SequenceEqual(directIds)).IsTrue();
    }

    [TestCase]
    public static void LoadBuildsANewRegistryOnEveryCall()
    {
        var first = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
        var second = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());

        AssertBool(ReferenceEquals(first, second)).IsFalse();
    }
}
