using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class BiomeRegistry
{
    // Signature profiles for each biome (Solidum, Febris, Ordinem, Lumines, Varias, Inertiae, Subsidium, Spatium)
    // Plains: neutral, slightly ordered, helpful
    private static readonly float[] PlainsSignature = [0f, 0f, 0.2f, 0.1f, 0f, 0f, 0.2f, 0f];
    // Forest: cool, chaotic, dark, organic, helpful
    private static readonly float[] ForestSignature = [0f, -0.3f, -0.3f, -0.3f, 0f, 0.2f, 0.3f, 0f];
    // Desert: hot, solid, ordered, bright
    private static readonly float[] DesertSignature = [0.3f, 0.7f, 0.3f, 0.4f, 0f, -0.2f, -0.2f, 0.3f];
    // Tundra: cold, ordered, bright
    private static readonly float[] TundraSignature = [0.2f, -0.7f, 0.5f, 0.3f, 0f, 0.1f, 0f, -0.1f];
    // Swamp: cool, chaotic, dark, heavy
    private static readonly float[] SwampSignature = [-0.2f, -0.2f, -0.5f, -0.5f, 0f, -0.3f, -0.1f, 0f];
    // Mountains: very solid, cold, ordered, high
    private static readonly float[] MountainsSignature = [0.7f, -0.2f, 0.4f, 0.2f, 0f, 0.4f, 0f, 0.3f];

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
        RegisterPlains();
        RegisterForest();
        RegisterDesert();
        RegisterTundra();
        RegisterSwamp();
        RegisterMountains();
    }

    private void RegisterPlains()
    {
        var passable = new TilePool();
        passable.Add("plains_grass", 0.40f);
        passable.Add("plains_grass_tall", 0.25f);
        passable.Add("plains_wildflowers", 0.15f);
        passable.Add("plains_fertile_soil", 0.10f);
        passable.Add("plains_path", 0.10f);

        var blocked = new TilePool();
        blocked.Add("plains_small_rock", 0.40f);
        blocked.Add("plains_boulder", 0.30f);
        blocked.Add("plains_shrub", 0.30f);

        Register(new BiomeDefinition(
            BiomeType.Plains,
            new CardSignature(PlainsSignature),
            passable,
            blocked,
            0.15f)); // 15% blocked - open terrain
    }

    private void RegisterForest()
    {
        var passable = new TilePool();
        passable.Add("forest_floor", 0.30f);
        passable.Add("forest_moss", 0.25f);
        passable.Add("forest_leaves", 0.20f);
        passable.Add("forest_clearing", 0.15f);
        passable.Add("forest_undergrowth", 0.10f);

        var blocked = new TilePool();
        blocked.Add("forest_tree", 0.35f);
        blocked.Add("forest_dense_trees", 0.25f);
        blocked.Add("forest_fallen_log", 0.15f);
        blocked.Add("forest_mushrooms", 0.10f);
        blocked.Add("forest_stump", 0.15f);

        Register(new BiomeDefinition(
            BiomeType.Forest,
            new CardSignature(ForestSignature),
            passable,
            blocked,
            0.40f)); // 40% blocked - dense forest
    }

    private void RegisterDesert()
    {
        var passable = new TilePool();
        passable.Add("desert_sand", 0.35f);
        passable.Add("desert_dune", 0.25f);
        passable.Add("desert_cracked", 0.15f);
        passable.Add("desert_gravel", 0.15f);
        passable.Add("desert_rocky", 0.10f);

        var blocked = new TilePool();
        blocked.Add("desert_rock", 0.30f);
        blocked.Add("desert_outcrop", 0.25f);
        blocked.Add("desert_cactus", 0.25f);
        blocked.Add("desert_bones", 0.10f);
        blocked.Add("desert_skull", 0.10f);

        Register(new BiomeDefinition(
            BiomeType.Desert,
            new CardSignature(DesertSignature),
            passable,
            blocked,
            0.12f)); // 12% blocked - sparse obstacles
    }

    private void RegisterTundra()
    {
        var passable = new TilePool();
        passable.Add("tundra_snow", 0.35f);
        passable.Add("tundra_packed_snow", 0.25f);
        passable.Add("tundra_frost", 0.15f);
        passable.Add("tundra_permafrost", 0.15f);
        passable.Add("tundra_ice_path", 0.10f);

        var blocked = new TilePool();
        blocked.Add("tundra_ice_block", 0.25f);
        blocked.Add("tundra_frozen_rock", 0.25f);
        blocked.Add("tundra_evergreen", 0.20f);
        blocked.Add("tundra_frozen_lake", 0.15f);
        blocked.Add("tundra_snowdrift", 0.15f);

        Register(new BiomeDefinition(
            BiomeType.Tundra,
            new CardSignature(TundraSignature),
            passable,
            blocked,
            0.22f)); // 22% blocked
    }

    private void RegisterSwamp()
    {
        var passable = new TilePool();
        passable.Add("swamp_mud", 0.30f);
        passable.Add("swamp_shallow_water", 0.25f);
        passable.Add("swamp_reeds", 0.20f);
        passable.Add("swamp_moss", 0.15f);
        passable.Add("swamp_peat", 0.10f);

        var blocked = new TilePool();
        blocked.Add("swamp_murky_pool", 0.30f);
        blocked.Add("swamp_dead_tree", 0.25f);
        blocked.Add("swamp_gnarled_roots", 0.20f);
        blocked.Add("swamp_lily_pads", 0.15f);
        blocked.Add("swamp_fog_hollow", 0.10f);

        Register(new BiomeDefinition(
            BiomeType.Swamp,
            new CardSignature(SwampSignature),
            passable,
            blocked,
            0.35f)); // 35% blocked - treacherous terrain
    }

    private void RegisterMountains()
    {
        var passable = new TilePool();
        passable.Add("mountains_rock", 0.30f);
        passable.Add("mountains_gravel", 0.25f);
        passable.Add("mountains_slate", 0.20f);
        passable.Add("mountains_scree", 0.15f);
        passable.Add("mountains_alpine_grass", 0.10f);

        var blocked = new TilePool();
        blocked.Add("mountains_cliff", 0.30f);
        blocked.Add("mountains_boulder", 0.30f);
        blocked.Add("mountains_ore", 0.15f);
        blocked.Add("mountains_alpine_shrub", 0.15f);
        blocked.Add("mountains_cave_entrance", 0.10f);

        Register(new BiomeDefinition(
            BiomeType.Mountains,
            new CardSignature(MountainsSignature),
            passable,
            blocked,
            0.38f)); // 38% blocked - rugged terrain
    }
}
