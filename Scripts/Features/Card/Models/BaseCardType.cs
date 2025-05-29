using System;
using Godot;
using Godot.Collections;
using Array = System.Array;

namespace CardCleaner.Scripts;

public enum CardCategory
{
    Playstyle,   // Affects autonomous agent behavior
    Equipment,   // Gear for the virtual character
    Skill        // Attacks and abilities
}

public enum CardRarity
{
    Common,
    Uncommon, 
    Rare,
    Epic,
    Legendary
}

[Tool]
[GlobalClass]
public partial class BaseCardType : Resource
{
    [Export] public string TypeName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public CardCategory Category { get; set; } = CardCategory.Skill;
    [Export] public CardRarity BaseRarity { get; set; } = CardRarity.Common;
    
    [Export] public CardSignature BaseSignature { get; set; } = new();
    [Export] public float MatchRadius { get; set; } = 0.5f; // How far signatures can be to match this base
    
    // Visual assets specific to this card type
    [Export] public Texture2D[] ArtOptions { get; set; } = {  };
    [Export] public Texture2D[] SymbolOptions { get; set; } = {};

    // New exports for energy fill based on base type
    [Export] public Texture2D[] EnergyFill1Options { get; set; } = { };
    [Export] public Texture2D[] EnergyFill2Options { get; set; } = { };
    
    // Base stats before residual energy modifiers
    [Export] public float BasePower { get; set; } = 1.0f;
    [Export] public float BaseValue { get; set; } = 10.0f; // Monetary value
    [Export] public int BaseCost { get; set; } = 1; // Energy/mana cost
    
    // Residual energy modifiers - how signature differences affect this card
    [Export] public Array<ResidualEnergyModifier> Modifiers { get; set; } = new();
    
    // Base effect description template (will be modified by residual energy)
    [Export] public string BaseEffectTemplate { get; set; } = "";

    public bool CanMatch(CardSignature signature, float maxDistance = -1f)
    {
        float distance = signature.DistanceTo(BaseSignature);
        float threshold = maxDistance > 0 ? maxDistance : MatchRadius;
        return distance <= threshold;
    }

    public float CalculateMatchWeight(CardSignature signature)
    {
        float distance = signature.DistanceTo(BaseSignature);
        if (distance > MatchRadius) return 0f;
        
        // Closer signatures get higher weight (inverse distance)
        return 1f / (1f + distance);
    }

    public CardRarity CalculateActualRarity(CardSignature signature)
    {
        // Base rarity can be modified by how unusual the signature is
        CardSignature residual = signature.Subtract(BaseSignature);
        float unusualness = CalculateUnusualness(residual);
        
        int rarityBoost = Mathf.FloorToInt(unusualness * 2f); // 0-2 rarity levels boost
        int newRarity = Math.Min((int)BaseRarity + rarityBoost, (int)CardRarity.Legendary);
        
        return (CardRarity)newRarity;
    }

    public float CalculateActualValue(CardSignature signature)
    {
        CardRarity actualRarity = CalculateActualRarity(signature);
        float rarityMultiplier = (int)actualRarity + 1f; // Common=1x, Legendary=5x
        
        CardSignature residual = signature.Subtract(BaseSignature);
        float powerModifier = CalculatePowerModifier(residual);
        
        return BaseValue * rarityMultiplier * (1f + powerModifier * 0.5f);
    }

    private float CalculateUnusualness(CardSignature residual)
    {
        // How far the residual energy deviates from the base
        float totalDeviation = 0f;
        for (int i = 0; i < 8; i++)
            totalDeviation += Mathf.Abs(residual[i]);
        
        return Mathf.Clamp(totalDeviation / 4f, 0f, 1f); // Normalize to 0-1
    }

    private float CalculatePowerModifier(CardSignature residual)
    {
        float totalMagnitude = 0f;
        foreach (var modifier in Modifiers)
            totalMagnitude += modifier.CalculateEffect(residual);
        
        return totalMagnitude;
    }
}