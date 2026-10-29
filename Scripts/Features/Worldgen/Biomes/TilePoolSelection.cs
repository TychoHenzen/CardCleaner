using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Draws tile IDs from a <see cref="TilePool"/> with an rng. Kept apart from the data so the pool and the biome
/// stay pure data; every draw is one <see cref="IRandom.Randf"/> call.
/// </summary>
internal static class TilePoolSelection
{
    internal static string? SelectPassableTile(BiomeDefinition biome, IRandom rng) =>
        SelectRandom(biome.PassableTiles, rng);

    internal static string? SelectRandom(TilePool pool, IRandom rng)
    {
        if (pool.Entries.Count == 0)
            return null;

        if (pool.TotalWeight <= 0)
            return pool.Entries[0].TileId;

        var roll = rng.Randf() * pool.TotalWeight;
        var cumulative = 0f;

        foreach (var entry in pool.Entries)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative)
                return entry.TileId;
        }

        return pool.Entries[pool.Entries.Count - 1].TileId;
    }
}
