using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
///     The only place that loads the tile data files into a TileRegistry.
/// </summary>
public static class TileCatalogLoad
{
    public static TileRegistry Load(IAssetPaths paths, ICompiledAtlasSource atlas)
    {
        var registry = new TileRegistry();
        LoadInto(registry, paths, atlas);
        return registry;
    }

    /// <summary>
    ///     Loads the tile data at <paramref name="path"/> (the default file when null) and registers its tiles on top
    ///     of the registry's current ones.
    /// </summary>
    public static void LoadInto(
        TileRegistry registry,
        IAssetPaths paths,
        ICompiledAtlasSource atlas,
        string? path = null,
        string? atlasMappingPath = null)
    {
        var loaded = TileDataLoader.LoadTileRegistry(paths, path);
        var original = new TileVisualLoader.OriginalTileset(loaded.TilesetPath, loaded.Tiles, loaded.TilesetConfig);
        registry.Install(TileVisualLoader.Load(original, atlasMappingPath, atlas));
    }

    /// <summary>
    ///     Clears the registry, then loads the tile data at <paramref name="path"/> into it.
    /// </summary>
    public static void Reload(
        TileRegistry registry,
        IAssetPaths paths,
        ICompiledAtlasSource atlas,
        string? path = null,
        string? atlasMappingPath = null)
    {
        registry.Clear();
        LoadInto(registry, paths, atlas, path, atlasMappingPath);
    }
}
