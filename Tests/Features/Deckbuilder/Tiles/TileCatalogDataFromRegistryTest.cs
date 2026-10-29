using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Tiles;

/// <summary>
/// Pins the registry constructor of TileCatalogData, the one the [Service] scan picks. For the shipped registry it must
/// yield the same tile order and the same variation groups as the two-argument constructor fed by the registry's own
/// queries, which is the path the catalog took before the scan built it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileCatalogDataFromRegistryTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void RegistryConstructorMatchesTheTwoArgumentPathForTheShippedRegistry()
    {
        var registry = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
        var fromRegistry = new TileCatalogData(registry);
        var fromQueries = new TileCatalogData(registry.GetAllTiles(), registry.GetAllVariationGroups());

        var tileIds = fromQueries.GetAllTiles().Select(tile => tile.Id).ToList();
        AssertBool(tileIds.Count > 0).IsTrue();
        AssertThat(fromRegistry.GetAllTiles().Select(tile => tile.Id).ToArray()).IsEqual(tileIds.ToArray());
        AssertThat(Describe(fromRegistry.GetAllVariationGroups()))
            .IsEqual(Describe(fromQueries.GetAllVariationGroups()));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FromReturnsTheSameSnapshotAsTheRegistryConstructor()
    {
        var registry = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
        var viaFrom = TileCatalogData.From(registry);
        var viaConstructor = new TileCatalogData(registry);

        AssertThat(viaFrom.GetAllTiles().Select(tile => tile.Id).ToArray())
            .IsEqual(viaConstructor.GetAllTiles().Select(tile => tile.Id).ToArray());
        AssertThat(Describe(viaFrom.GetAllVariationGroups())).IsEqual(Describe(viaConstructor.GetAllVariationGroups()));
    }

    private static string[] Describe(IEnumerable<VariationGroup> groups) =>
        groups.Select(group =>
            $"{group.BaseName}|{group.Mode}|{group.MaxWeight}|"
            + string.Join(",", group.Variants.Select(variant => $"{variant.TileId}:{variant.Weight}")))
            .ToArray();
}
