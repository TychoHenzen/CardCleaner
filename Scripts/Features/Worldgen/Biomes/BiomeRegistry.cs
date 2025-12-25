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
        // Plains: open grasslands with scattered rocks
        var plainsPassable = new TilePool();
        plainsPassable.Add("grass", 0.85f);
        plainsPassable.Add("dirt", 0.15f);
        var plainsBlocked = new TilePool();
        plainsBlocked.Add("stone", 0.7f);
        plainsBlocked.Add("wall", 0.3f);

        Register(new BiomeDefinition(
            BiomeType.Plains,
            new CardSignature(PlainsSignature),
            plainsPassable,
            plainsBlocked,
            0.20f));

        // Forest: dense tree coverage on grass/dirt floor
        var forestPassable = new TilePool();
        forestPassable.Add("grass", 0.70f);
        forestPassable.Add("dirt", 0.30f);
        var forestBlocked = new TilePool();
        forestBlocked.Add("wall", 0.85f);
        forestBlocked.Add("stone", 0.15f);

        Register(new BiomeDefinition(
            BiomeType.Forest,
            new CardSignature(ForestSignature),
            forestPassable,
            forestBlocked,
            0.40f));

        // Desert: sandy terrain with rocky outcrops
        var desertPassable = new TilePool();
        desertPassable.Add("dirt", 0.90f);
        desertPassable.Add("floor", 0.10f);
        var desertBlocked = new TilePool();
        desertBlocked.Add("stone", 0.90f);
        desertBlocked.Add("wall", 0.10f);

        Register(new BiomeDefinition(
            BiomeType.Desert,
            new CardSignature(DesertSignature),
            desertPassable,
            desertBlocked,
            0.15f));

        // Tundra: icy/snowy terrain (floor as snow substitute) with frozen obstacles
        var tundraPassable = new TilePool();
        tundraPassable.Add("floor", 0.85f);
        tundraPassable.Add("dirt", 0.15f);
        var tundraBlocked = new TilePool();
        tundraBlocked.Add("water", 0.50f);
        tundraBlocked.Add("stone", 0.35f);
        tundraBlocked.Add("wall", 0.15f);

        Register(new BiomeDefinition(
            BiomeType.Tundra,
            new CardSignature(TundraSignature),
            tundraPassable,
            tundraBlocked,
            0.25f));
    }
}
