using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class EnemySpawnData : Resource
{
    // Default values as constants
    private const string DefaultEnemyName = "";
    private const float DefaultSpawnWeight = 1.0f;
    private const float DefaultMinSignatureIntensity = 0.3f;
    private const float DefaultMaxSignatureIntensity = 1.0f;
    private const bool DefaultAcceptsAnyTerrain = true;
    private const float DefaultTerrainMatchBonus = 2.0f;
    private const int DefaultSourceId = 0;
    private const string DefaultEnemyScenePath = "";
    private static readonly CardSignature DefaultBaseSignature = new();
    private static readonly Vector2I DefaultAtlasCoords = Vector2I.Zero;

    [Export] public string EnemyName { get; set; } = DefaultEnemyName;
    [Export] public CardSignature BaseSignature { get; set; } = new();
    [Export] public float SpawnWeight { get; set; } = DefaultSpawnWeight;
    [Export] public float MinSignatureIntensity { get; set; } = DefaultMinSignatureIntensity;
    [Export] public float MaxSignatureIntensity { get; set; } = DefaultMaxSignatureIntensity;

    // Terrain requirements - now using CompatibilityTag arrays
    [JsonIgnore] public Array<CompatibilityTag> PreferredTerrain { get; set; } = new();
    [Export] public Array<string> PreferredTerrainNames { get; set; } = new();
    [Export] public bool AcceptsAnyTerrain { get; set; } = DefaultAcceptsAnyTerrain;
    [Export] public float TerrainMatchBonus { get; set; } = DefaultTerrainMatchBonus;

    // Visual representation on tilemap
    [Export] public int SourceId { get; set; } = DefaultSourceId;
    [Export] public Vector2I AtlasCoords { get; set; } = DefaultAtlasCoords;

    // Runtime spawn data
    [JsonIgnore] public PackedScene? EnemyScene { get; set; }
    [Export] public string EnemyScenePath { get; set; } = DefaultEnemyScenePath;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(EnemyName) => true,
            nameof(BaseSignature) => true,
            nameof(SpawnWeight) => true,
            nameof(MinSignatureIntensity) => true,
            nameof(MaxSignatureIntensity) => true,
            nameof(AcceptsAnyTerrain) => true,
            nameof(TerrainMatchBonus) => true,
            nameof(SourceId) => true,
            nameof(AtlasCoords) => true,
            nameof(EnemyScenePath) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(EnemyName) => DefaultEnemyName,
            nameof(BaseSignature) => Variant.From(DefaultBaseSignature),
            nameof(SpawnWeight) => DefaultSpawnWeight,
            nameof(MinSignatureIntensity) => DefaultMinSignatureIntensity,
            nameof(MaxSignatureIntensity) => DefaultMaxSignatureIntensity,
            nameof(AcceptsAnyTerrain) => DefaultAcceptsAnyTerrain,
            nameof(TerrainMatchBonus) => DefaultTerrainMatchBonus,
            nameof(SourceId) => DefaultSourceId,
            nameof(AtlasCoords) => Variant.From(DefaultAtlasCoords),
            nameof(EnemyScenePath) => DefaultEnemyScenePath,
            _ => base._PropertyGetRevert(property)
        };
    }

    public bool CanSpawnOnTile(SemanticTile tile, CardSignature blendedSignature)
    {
        // Check terrain compatibility
        if (!AcceptsAnyTerrain && !CompatibilityTag.IsArrayCompatibleWith(PreferredTerrain, tile.SocketData.Up))
            return false;

        // Check signature intensity
        var intensity = CalculateSignatureIntensity(blendedSignature);
        return intensity >= MinSignatureIntensity && intensity <= MaxSignatureIntensity;
    }

    public float CalculateSpawnWeight(SemanticTile tile, CardSignature blendedSignature)
    {
        if (!CanSpawnOnTile(tile, blendedSignature))
            return 0f;

        var weight = SpawnWeight;

        // Bonus for terrain match
        if (CompatibilityTag.IsArrayCompatibleWith(PreferredTerrain, tile.SocketData.Up))
            weight *= TerrainMatchBonus;

        // Weight by signature similarity
        var similarity = 1f - BaseSignature.DistanceTo(blendedSignature) / 4f; // Normalize to 0-1
        weight *= Mathf.Max(0.1f, similarity);

        return weight;
    }

    private static float CalculateSignatureIntensity(CardSignature signature)
    {
        var totalIntensity = 0f;
        for (var i = 0; i < 8; i++)
            totalIntensity += Mathf.Abs(signature[i]);
        return totalIntensity / 8f;
    }
}