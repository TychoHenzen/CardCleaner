using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Resolves auto-tile variants for a map based on registered AutoTileConfigs.
/// </summary>
public class AutoTileResolver
{
    private readonly Dictionary<string, AutoTileConfig> _configsByBaseTile = new();

    /// <summary>
    ///     Create a resolver with no configs (add later via Register).
    /// </summary>
    public AutoTileResolver()
    {
    }

    /// <summary>
    ///     Create a resolver with initial configs.
    /// </summary>
    public AutoTileResolver(IEnumerable<AutoTileConfig> configs)
    {
        foreach (var config in configs) Register(config);
    }

    /// <summary>
    ///     Get the count of registered configs.
    /// </summary>
    public int ConfigCount => _configsByBaseTile.Count;

    /// <summary>
    ///     Register an auto-tile configuration.
    /// </summary>
    public void Register(AutoTileConfig config)
    {
        if (string.IsNullOrEmpty(config.BaseTileId))
            return;

        _configsByBaseTile[config.BaseTileId] = config;
    }

    /// <summary>
    ///     Check if a tile ID has an auto-tile config registered.
    /// </summary>
    public bool HasConfig(string tileId) => _configsByBaseTile.ContainsKey(tileId);

    /// <summary>
    ///     Get the auto-tile config for a base tile ID, or null if none.
    /// </summary>
    public AutoTileConfig? GetConfig(string tileId) => _configsByBaseTile.GetValueOrDefault(tileId);

    /// <summary>
    ///     Resolve the variant tile ID for a position in a map.
    /// </summary>
    /// <param name="position">The tile position</param>
    /// <param name="baseTileId">The current tile ID at this position</param>
    /// <param name="tileIds">2D array of tile IDs [y, x]</param>
    /// <param name="mapSize">Size of the map</param>
    /// <returns>Variant tile ID, or original tile ID if no config or no variant</returns>
    public string Resolve(Vector2I position, string baseTileId, string[,] tileIds, Vector2I mapSize)
    {
        var config = GetConfig(baseTileId);
        if (config == null)
            return baseTileId;

        var bitmask = NeighborBitmask.Compute(position, baseTileId, tileIds, mapSize);
        return config.GetVariant(bitmask);
    }

    /// <summary>
    ///     Apply auto-tiling to an entire map, modifying the tileIds array in place.
    /// </summary>
    /// <param name="tileIds">2D array of tile IDs [y, x] - will be modified</param>
    /// <param name="mapSize">Size of the map</param>
    /// <returns>Number of tiles that were replaced with variants</returns>
    public int ApplyToMap(string[,] tileIds, Vector2I mapSize)
    {
        var replacements = 0;

        // First pass: compute all variants (don't modify yet to ensure consistent neighbor checks)
        var variants = new string[mapSize.Y, mapSize.X];
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
        {
            var position = new Vector2I(x, y);
            var baseTileId = tileIds[y, x];
            variants[y, x] = Resolve(position, baseTileId, tileIds, mapSize);
        }

        // Second pass: apply variants
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
            if (variants[y, x] != tileIds[y, x])
            {
                tileIds[y, x] = variants[y, x];
                replacements++;
            }

        return replacements;
    }

    /// <summary>
    ///     Get all registered configs.
    /// </summary>
    public IEnumerable<AutoTileConfig> GetAllConfigs() => _configsByBaseTile.Values;
}
