using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Features.Shop.Models;
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
    public static void EveryAtlasIsAFileTheSyncManifestCopies()
    {
        var manifestPath = ProjectSettings.GlobalizePath("res://tools/synty-assets.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var targets = manifest.RootElement.GetProperty("files")
            .EnumerateArray()
            .Select(file => file.GetProperty("target").GetString())
            .ToHashSet();

        var missing = PackMaterialCatalog.AtlasPaths
            .Where(atlas => !targets.Contains(atlas[SyntyRoot.Length..]))
            .ToList();

        AssertThat(string.Join(", ", missing)).IsEmpty();
        AssertThat(PackMaterialCatalog.AtlasPaths.Count()).IsEqual(3);
    }
}
