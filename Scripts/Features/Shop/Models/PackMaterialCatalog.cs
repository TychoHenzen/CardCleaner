using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Which colour atlas belongs to which Synty pack. Every mesh of a pack shares one palette texture, and
///     the FBX files point at source texture files that do not ship, so the atlas has to be applied by us.
///     The atlas files are the ones tools/synty-assets.json copies next to the meshes. An art path outside these
///     pack folders (the particle textures, our own models) has no atlas.
/// </summary>
public static class PackMaterialCatalog
{
    private const string SyntyRoot = "res://Assets/Synty/";

    private static readonly (string Folder, string Atlas)[] Packs =
    [
        ("SimpleShopInterior", "SimpleShopInterior_Texture.png"),
        ("PolygonOffice", "PolygonOffice_Texture_01_A.png"),
        ("PolygonDungeon", "Dungeons_Texture_01.png")
    ];

    /// <summary>The atlas file of every known pack.</summary>
    public static IEnumerable<string> AtlasPaths => Packs.Select(pack => AtlasPath(pack.Folder, pack.Atlas));

    /// <summary>The atlas for a mesh under one of the known pack folders, or null for any other path.</summary>
    public static string? AtlasPathFor(string artPath)
    {
        foreach (var (folder, atlas) in Packs)
            if (artPath.StartsWith($"{SyntyRoot}{folder}/", StringComparison.Ordinal))
                return AtlasPath(folder, atlas);

        return null;
    }

    private static string AtlasPath(string folder, string atlas) => $"{SyntyRoot}{folder}/{atlas}";
}
