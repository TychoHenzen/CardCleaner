using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class EnemySpawnData : Resource
{
    [Export] public string EnemyName { get; set; } = "";
    [Export] public CardSignature BaseSignature { get; set; } = new();
    [Export] public float SpawnWeight { get; set; } = 1.0f;
    [Export] public float MinSignatureIntensity { get; set; } = 0.3f;
    [Export] public float MaxSignatureIntensity { get; set; } = 1.0f;
    
    // Terrain requirements
    [Export] public SocketType PreferredTerrain { get; set; } = SocketType.Any;
    [Export] public float TerrainMatchBonus { get; set; } = 2.0f;
    
    // Visual representation on tilemap
    [Export] public int SourceId { get; set; } = 0;
    [Export] public Vector2I AtlasCoords { get; set; }
    
    // Runtime spawn data
    [Export] public PackedScene? EnemyScene { get; set; }
    
    public bool CanSpawnOnTile(SemanticTile tile, CardSignature blendedSignature)
    {
        // Check terrain compatibility
        if (PreferredTerrain != SocketType.Any && !SemanticTile.SocketsCompatible(PreferredTerrain, tile.Up))
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
        if (SemanticTile.SocketsCompatible(PreferredTerrain, tile.Up))
            weight *= TerrainMatchBonus;
            
        // Weight by signature similarity
        var similarity = 1f - BaseSignature.DistanceTo(blendedSignature) / 4f; // Normalize to 0-1
        weight *= Mathf.Max(0.1f, similarity);
        
        return weight;
    }
    
    private static float CalculateSignatureIntensity(CardSignature signature)
    {
        var totalIntensity = 0f;
        for (int i = 0; i < 8; i++)
            totalIntensity += Mathf.Abs(signature[i]);
        return totalIntensity / 8f;
    }
}