using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Building;

/// <summary>
///     Which colour atlas belongs to which Synty pack folder. Every mesh of a pack shares one palette texture, and
///     the FBX files point at source texture files that do not ship, so the atlas has to be applied by us.
///     The atlas files sit next to the meshes in the Assets submodule. A pack has a folder under
///     <c>res://Assets/Synty/</c> from the first exports, and the asset manager's Send to Project puts later exports
///     under <c>res://Assets/Models/</c> at their path in the asset library, so one pack can have a folder in each.
///     An art path outside these folders (the particle textures, our own models) has no atlas.
/// </summary>
public static class PackMaterialCatalog
{
    private const string SyntyRoot = "res://Assets/Synty/";
    private const string LibraryRoot = "res://Assets/Models/";

    private static readonly (string Folder, string Atlas)[] Packs =
    [
        ($"{SyntyRoot}SimpleShopInterior/", "SimpleShopInterior_Texture.png"),
        ($"{LibraryRoot}synty/props/SIMPLE_Shop_Interiors/SourceFiles/Fbx/", "SimpleShopInterior_Texture.png"),
        ($"{SyntyRoot}PolygonOffice/", "PolygonOffice_Texture_01_A.png"),
        ($"{SyntyRoot}PolygonDungeon/", "Dungeons_Texture_01.png"),
        ($"{SyntyRoot}PolygonMiniCity/", "PolygonMinis_Texture_01.png"),
        ($"{SyntyRoot}PolygonSurvivalWorkshop/", "PolygonSurvivalWorkshop_Texture.png"),
        ($"{SyntyRoot}PolygonSurvivalTools/", "PolygonSurvivalTools_Texture.png"),
        ($"{SyntyRoot}PolygonSurvivalConstruction/", "PolygonSurvivalConstruction_Texture.png")
    ];

    /// <summary>The folders whose meshes get an atlas, each ending in a slash.</summary>
    public static IEnumerable<string> PackFolders => Packs.Select(pack => pack.Folder);

    /// <summary>The atlas file of every known pack folder.</summary>
    public static IEnumerable<string> AtlasPaths =>
        Packs.Select(pack => pack.Folder + pack.Atlas);

    /// <summary>
    ///     The atlas for a mesh directly in one of the known pack folders, or null
    ///     for any other path.
    /// </summary>
    public static string? AtlasPathFor(string artPath)
    {
        foreach (var (folder, atlas) in Packs)
        {
            if (artPath.StartsWith(folder, StringComparison.Ordinal))
                return folder + atlas;
        }

        return null;
    }
}
