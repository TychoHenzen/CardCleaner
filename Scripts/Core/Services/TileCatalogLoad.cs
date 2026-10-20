using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
///     The only place that constructs a TileRegistry in Scripts.
/// </summary>
public static class TileCatalogLoad
{
    public static TileRegistry Load(IAssetPaths paths, ICompiledAtlasSource atlas) => new(paths, atlas);
}
