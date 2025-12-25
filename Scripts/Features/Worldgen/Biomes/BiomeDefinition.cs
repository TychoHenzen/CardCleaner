using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

[Tool]
[GlobalClass]
public partial class BiomeDefinition : Resource
{
    public BiomeDefinition() { }

    public BiomeDefinition(
        BiomeType type,
        CardSignature affinitySignature,
        TilePool passableTiles,
        TilePool blockedTiles,
        float blockedPercentage = 0.3f)
    {
        Type = type;
        AffinitySignature = affinitySignature;
        PassableTiles = passableTiles;
        BlockedTiles = blockedTiles;
        BlockedPercentage = Mathf.Clamp(blockedPercentage, 0f, 1f);
    }

    [Export] public BiomeType Type { get; set; } = BiomeType.Plains;

    [Export] public CardSignature AffinitySignature { get; set; } = new();

    [Export] public TilePool PassableTiles { get; set; } = new();

    [Export] public TilePool BlockedTiles { get; set; } = new();

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BlockedPercentage { get; set; } = 0.3f;

    public string? SelectPassableTile(RandomNumberGenerator rng) => PassableTiles.SelectRandom(rng);

    public string? SelectBlockedTile(RandomNumberGenerator rng) => BlockedTiles.SelectRandom(rng);
}
