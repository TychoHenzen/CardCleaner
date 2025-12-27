using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

[Tool]
[GlobalClass]
public partial class BiomeDefinition : Resource
{
    // Default values as constants
    private const BiomeType DefaultType = BiomeType.Plains;
    private const float DefaultBlockedPercentage = 0.3f;
    private static readonly CardSignature DefaultAffinitySignature = new();

    public BiomeDefinition() { }

    public BiomeDefinition(
        BiomeType type,
        CardSignature affinitySignature,
        TilePool passableTiles,
        TilePool blockedTiles,
        float blockedPercentage = DefaultBlockedPercentage)
    {
        Type = type;
        AffinitySignature = affinitySignature;
        PassableTiles = passableTiles;
        BlockedTiles = blockedTiles;
        BlockedPercentage = Mathf.Clamp(blockedPercentage, 0f, 1f);
    }

    [Export] public BiomeType Type { get; set; } = DefaultType;

    [Export] public CardSignature AffinitySignature { get; set; } = new();

    [Export] public TilePool PassableTiles { get; set; } = new();

    [Export] public TilePool BlockedTiles { get; set; } = new();

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BlockedPercentage { get; set; } = DefaultBlockedPercentage;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Type) => true,
            nameof(AffinitySignature) => true,
            nameof(BlockedPercentage) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Type) => (int)DefaultType,
            nameof(AffinitySignature) => Variant.From(DefaultAffinitySignature),
            nameof(BlockedPercentage) => DefaultBlockedPercentage,
            _ => base._PropertyGetRevert(property)
        };
    }

    public string? SelectPassableTile(RandomNumberGenerator rng) => PassableTiles.SelectRandom(rng);

    public string? SelectBlockedTile(RandomNumberGenerator rng) => BlockedTiles.SelectRandom(rng);
}
