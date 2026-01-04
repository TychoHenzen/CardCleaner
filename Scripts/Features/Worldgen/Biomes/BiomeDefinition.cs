using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

[Tool]
[GlobalClass]
public partial class BiomeDefinition : Resource
{
    // Default values as constants
    private const string DefaultId = "plains";
    private const float DefaultBlockedPercentage = 0.3f;
    private static readonly CardSignature DefaultAffinitySignature = new();

    public BiomeDefinition() { }

    public BiomeDefinition(
        string id,
        CardSignature affinitySignature,
        TilePool passableTiles,
        TilePool blockedTiles,
        float blockedPercentage = DefaultBlockedPercentage)
    {
        Id = id;
        AffinitySignature = affinitySignature;
        PassableTiles = passableTiles;
        BlockedTiles = blockedTiles;
        BlockedPercentage = Mathf.Clamp(blockedPercentage, 0f, 1f);
    }

    /// <summary>
    /// Factory method to create BiomeDefinition from JSON data
    /// </summary>
    public static BiomeDefinition FromData(string biomeId, BiomeData data)
    {
        var signature = new CardSignature(data.Signature);

        var passableTiles = new TilePool();
        foreach (var (tileId, weight) in data.PassableTiles)
            passableTiles.Add(tileId, weight);

        var blockedTiles = new TilePool();
        foreach (var (tileId, weight) in data.BlockedTiles)
            blockedTiles.Add(tileId, weight);

        return new BiomeDefinition(
            biomeId,
            signature,
            passableTiles,
            blockedTiles,
            data.BlockedPercentage);
    }

    [Export] public string Id { get; set; } = DefaultId;

    [Export] public CardSignature AffinitySignature { get; set; } = new();

    [Export] public TilePool PassableTiles { get; set; } = new();

    [Export] public TilePool BlockedTiles { get; set; } = new();

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BlockedPercentage { get; set; } = DefaultBlockedPercentage;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Id) => true,
            nameof(AffinitySignature) => true,
            nameof(BlockedPercentage) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Id) => DefaultId,
            nameof(AffinitySignature) => Variant.From(DefaultAffinitySignature),
            nameof(BlockedPercentage) => DefaultBlockedPercentage,
            _ => base._PropertyGetRevert(property)
        };
    }

    public string? SelectPassableTile(RandomNumberGenerator rng) => PassableTiles.SelectRandom(rng);

    public string? SelectBlockedTile(RandomNumberGenerator rng) => BlockedTiles.SelectRandom(rng);
}
