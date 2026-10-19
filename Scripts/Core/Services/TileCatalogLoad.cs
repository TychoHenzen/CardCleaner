using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
///     The only place that constructs a TileRegistry in Scripts.
/// </summary>
public static class TileCatalogLoad
{
    public static TileRegistry Load(IAssetPaths paths) => new(paths);
}
