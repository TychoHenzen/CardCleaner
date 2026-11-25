using System.Linq;
using Godot;
using CardCleaner.Scripts.Features.Shop.Building;

namespace CardCleaner.Tests.Features.Shop.Building;

[TestSuite]
[RequireGodotRuntime]
public class PackMaterialCatalogTest
{
    private const string SyntyRoot = "res://Assets/Synty/";

    [TestCase]
    public static void ShopInteriorsMapToTheirAtlas()
    {
        var atlas = PackMaterialCatalog.AtlasPathFor($"{SyntyRoot}SimpleShopInterior/SI_Env_Wall_01.fbx");

        AssertThat(atlas).IsEqual($"{SyntyRoot}SimpleShopInterior/SimpleShopInterior_Texture.png");
    }

    [TestCase]
    public static void OfficePropsMapToTheirAtlas()
    {
        var atlas = PackMaterialCatalog.AtlasPathFor($"{SyntyRoot}PolygonOffice/SM_Prop_Desk_01.fbx");

        AssertThat(atlas).IsEqual($"{SyntyRoot}PolygonOffice/PolygonOffice_Texture_01_A.png");
    }

    [TestCase]
    public static void DungeonPiecesMapToTheirAtlas()
    {
        var atlas = PackMaterialCatalog.AtlasPathFor($"{SyntyRoot}PolygonDungeon/SM_Env_Wall_Archway_01.fbx");

        AssertThat(atlas).IsEqual($"{SyntyRoot}PolygonDungeon/Dungeons_Texture_01.png");
    }

    [TestCase]
    public static void PolygonMiniCityMapsToItsAtlas()
    {
        var atlas = PackMaterialCatalog.AtlasPathFor($"{SyntyRoot}PolygonMiniCity/SM_Bld_Shop_01.fbx");

        var miniCityAtlas = $"{SyntyRoot}PolygonMiniCity/PolygonMinis_Texture_01.png";

        AssertThat(atlas).IsEqual(miniCityAtlas);
        AssertThat(PackMaterialCatalog.AtlasPaths.ToList()).Contains(miniCityAtlas);
    }

    [TestCase]
    public static void AnAssetManagerExportOfAKnownPackMapsToTheAtlasBesideIt()
    {
        const string folder = "res://Assets/Models/synty/props/SIMPLE_Shop_Interiors/SourceFiles/Fbx/";

        var atlas = PackMaterialCatalog.AtlasPathFor($"{folder}SI_Prop_Shelf_09.fbx");

        AssertThat(atlas).IsEqual($"{folder}SimpleShopInterior_Texture.png");
    }

    [TestCase("")]
    [TestCase("res://Assets/Models/Button.fbx")]
    [TestCase("res://Assets/Models/synty/props/OtherPack/SI_Prop_Shelf_09.fbx")]
    [TestCase("res://Assets/Synty/ParticleFx/Generic_Circle_Soft_01.png")]
    [TestCase("res://Assets/Synty/SimpleShopInteriorExtras/SI_Env_Wall_01.fbx")]
    public static void PathsOutsideAKnownPackHaveNoAtlas(string artPath)
    {
        AssertThat(PackMaterialCatalog.AtlasPathFor(artPath)).IsNull();
    }

    [TestCase]
    public static void EveryAtlasIsAFileInTheAssetsSubmodule()
    {
        var missing = PackMaterialCatalog.AtlasPaths
            .Where(atlas => !FileAccess.FileExists(atlas))
            .ToList();

        AssertThat(string.Join(", ", missing)).IsEmpty();
        // An empty catalog would pass the existence check above without checking a single atlas.
        AssertThat(PackMaterialCatalog.AtlasPaths.ToList()).IsNotEmpty();
    }
}
