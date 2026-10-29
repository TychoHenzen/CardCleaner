using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Immutable, ordered set of biome definitions, the value WFC and the distribution calculator read.
/// Enumeration order is the order given to <see cref="From"/>; a later duplicate Id replaces the earlier
/// definition in place.
/// </summary>
public sealed record BiomeSet
{
    private readonly List<BiomeDefinition> _biomes;
    private readonly Dictionary<string, BiomeDefinition> _byId;

    private BiomeSet(IReadOnlyList<BiomeDefinition> biomes)
    {
        _biomes = new List<BiomeDefinition>(biomes);
        _byId = new Dictionary<string, BiomeDefinition>(_biomes.Count);
        foreach (var biome in _biomes)
            _byId[biome.Id] = biome;
    }

    /// <summary>A set with no biomes.</summary>
    public static BiomeSet Empty { get; } = new(new List<BiomeDefinition>());

    /// <summary>Builds a set that keeps the enumeration order of <paramref name="biomes"/>.</summary>
    public static BiomeSet From(IEnumerable<BiomeDefinition> biomes)
    {
        var ordered = new List<BiomeDefinition>();
        var indexById = new Dictionary<string, int>();
        foreach (var biome in biomes)
        {
            if (indexById.TryGetValue(biome.Id, out var index))
            {
                ordered[index] = biome;
            }
            else
            {
                indexById[biome.Id] = ordered.Count;
                ordered.Add(biome);
            }
        }

        return new BiomeSet(ordered);
    }

    public int Count => _biomes.Count;

    public IReadOnlyList<BiomeDefinition> Biomes => _biomes.AsReadOnly();

    public IEnumerable<string> Ids => _biomes.Select(biome => biome.Id);

    /// <summary>Returns the biome with <paramref name="id"/>, or null when the set has none.</summary>
    public BiomeDefinition? Get(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Returns the closest biome by affinity signature to <paramref name="s"/>, or null when empty.</summary>
    public BiomeDefinition? FindClosestBySignature(CardSignature s)
    {
        if (_biomes.Count == 0)
            return null;

        BiomeDefinition? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var biome in _biomes)
        {
            var distance = s.DistanceTo(biome.AffinitySignature);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = biome;
            }
        }

        return closest;
    }
}
