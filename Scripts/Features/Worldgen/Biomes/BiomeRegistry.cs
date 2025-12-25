using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class BiomeRegistry
{
    private static readonly float[] PlainsSignature = [0f, 0f, 0.2f, 0f, 0f, 0f, 0.1f, 0f];
    private static readonly float[] ForestSignature = [0f, -0.4f, -0.2f, -0.2f, 0f, 0.2f, 0.3f, 0f];
    private static readonly float[] DesertSignature = [0.3f, 0.6f, 0.3f, 0.2f, 0f, -0.2f, -0.1f, 0.2f];
    private static readonly float[] TundraSignature = [0.1f, -0.7f, 0.4f, 0.3f, 0f, 0.1f, 0f, -0.1f];
    private readonly Dictionary<BiomeType, BiomeDefinition> _biomes = new();

    public int Count => _biomes.Count;

    public void Register(BiomeDefinition biome) => _biomes[biome.Type] = biome;

    public BiomeDefinition? GetBiome(BiomeType type) => _biomes.GetValueOrDefault(type);

    public IEnumerable<BiomeDefinition> GetAllBiomes() => _biomes.Values;

    public BiomeDefinition? FindClosestBySignature(CardSignature signature)
    {
        if (_biomes.Count == 0)
            return null;

        BiomeDefinition? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var biome in _biomes.Values)
        {
            var distance = signature.DistanceTo(biome.AffinitySignature);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = biome;
            }
        }

        return closest;
    }

    public void Clear() => _biomes.Clear();

    public void RegisterDefaultBiomes()
    {
        // Plains: neutral signature, balanced terrain
        var plainsPassable = new TilePool();
        plainsPassable.Add("grass", 0.5f);
        plainsPassable.Add("floor", 0.3f);
        plainsPassable.Add("dirt", 0.2f);
        var plainsBlocked = new TilePool();
        plainsBlocked.Add("stone", 0.6f);
        plainsBlocked.Add("wall", 0.4f);

        Register(new BiomeDefinition(
            BiomeType.Plains,
            new CardSignature(PlainsSignature),
            plainsPassable,
            plainsBlocked,
            0.25f));

        // Forest: cool (Febris-), slightly chaotic, helpful
        var forestPassable = new TilePool();
        forestPassable.Add("grass", 0.6f);
        forestPassable.Add("dirt", 0.3f);
        forestPassable.Add("floor", 0.1f);
        var forestBlocked = new TilePool();
        forestBlocked.Add("wall", 0.7f);
        forestBlocked.Add("stone", 0.3f);

        Register(new BiomeDefinition(
            BiomeType.Forest,
            new CardSignature(ForestSignature),
            forestPassable,
            forestBlocked,
            0.35f));

        // Desert: hot (Febris+), solid, ordered
        var desertPassable = new TilePool();
        desertPassable.Add("dirt", 0.7f);
        desertPassable.Add("floor", 0.2f);
        desertPassable.Add("grass", 0.1f);
        var desertBlocked = new TilePool();
        desertBlocked.Add("stone", 0.8f);
        desertBlocked.Add("wall", 0.2f);

        Register(new BiomeDefinition(
            BiomeType.Desert,
            new CardSignature(DesertSignature),
            desertPassable,
            desertBlocked,
            0.20f));

        // Tundra: very cold (Febris--), ordered, light
        var tundraPassable = new TilePool();
        tundraPassable.Add("floor", 0.5f);
        tundraPassable.Add("dirt", 0.3f);
        tundraPassable.Add("grass", 0.2f);
        var tundraBlocked = new TilePool();
        tundraBlocked.Add("stone", 0.5f);
        tundraBlocked.Add("wall", 0.3f);
        tundraBlocked.Add("water", 0.2f);

        Register(new BiomeDefinition(
            BiomeType.Tundra,
            new CardSignature(TundraSignature),
            tundraPassable,
            tundraBlocked));
    }
}
