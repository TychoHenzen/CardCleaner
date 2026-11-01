using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class PackMaterialCatalogTest
{
    private const string SyntyRoot = "res://Assets/Synty/";

    [TestCase]
    [TestCategory("Unit")]
    public static void ShopInteriorsMapToTheirAtlas()
    {
        var atlas = PackMaterialCatalog.AtlasPathFor($"{SyntyRoot}SimpleShopInterior/SI_Env_Wall_01.fbx");

        AssertThat(atlas).IsEqual($"{SyntyRoot}SimpleShopInterior/SimpleShopInterior_Texture.png");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OfficePropsMapToTheirAtlas()
    {
        var atlas = PackMaterialCatalog.AtlasPathFor($"{SyntyRoot}PolygonOffice/SM_Prop_Desk_01.fbx");

        AssertThat(atlas).IsEqual($"{SyntyRoot}PolygonOffice/PolygonOffice_Texture_01_A.png");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DungeonPiecesMapToTheirAtlas()
    {
        var atlas = PackMaterialCatalog.AtlasPathFor($"{SyntyRoot}PolygonDungeon/SM_Env_Wall_Archway_01.fbx");

        AssertThat(atlas).IsEqual($"{SyntyRoot}PolygonDungeon/Dungeons_Texture_01.png");
    }

    [TestCase("")]
    [TestCase("res://Assets/Models/Button.fbx")]
    [TestCase("res://Assets/Synty/ParticleFx/Generic_Circle_Soft_01.png")]
    [TestCase("res://Assets/Synty/SimpleShopInteriorExtras/SI_Env_Wall_01.fbx")]
    [TestCategory("Unit")]
    public static void PathsOutsideAKnownPackHaveNoAtlas(string artPath)
    {
        AssertThat(PackMaterialCatalog.AtlasPathFor(artPath)).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryAtlasIsAFileInTheAssetsSubmodule()
    {
        var missing = PackMaterialCatalog.AtlasPaths
            .Where(atlas => !SyntyAssets.Exists(atlas[SyntyRoot.Length..]))
            .ToList();

        AssertThat(string.Join(", ", missing)).IsEmpty();
        // An empty catalog would pass the existence check above without checking a single atlas.
        AssertThat(PackMaterialCatalog.AtlasPaths.ToList()).IsNotEmpty();
    }
}
