using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Card.Models;

[Tool]
[GlobalClass]
public partial class BaseCardType : Resource
{
    // Default values as constants
    private const string DefaultTypeName = "";
    private const string DefaultDescription = "";
    private const CardCategory DefaultCategory = CardCategory.Skill;
    private const CardRarity DefaultBaseRarity = CardRarity.Common;
    private const float DefaultMatchRadius = 0.5f;
    private const float DefaultBasePower = 1.0f;
    private const float DefaultBaseValue = 10.0f;
    private const int DefaultBaseCost = 1;
    private const string DefaultBaseEffectTemplate = "";
    private static readonly CardSignature DefaultBaseSignature = new();

    [Export] public string TypeName { get; set; } = DefaultTypeName;
    [Export] public string Description { get; set; } = DefaultDescription;
    [Export] public CardCategory Category { get; set; } = DefaultCategory;
    [Export] public CardRarity BaseRarity { get; set; } = DefaultBaseRarity;

    [Export] public CardSignature BaseSignature { get; set; } = new();
    [Export] public float MatchRadius { get; set; } = DefaultMatchRadius; // How far signatures can be to match this base

    // Visual assets specific to this card type
    [Export] public Texture2D[] ArtOptions { get; set; } = [];
    [Export] public Texture2D[] SymbolOptions { get; set; } = [];

    // New exports for energy fill based on base type
    [Export] public Texture2D[] EnergyFill1Options { get; set; } = [];
    [Export] public Texture2D[] EnergyFill2Options { get; set; } = [];

    // Base stats before residual energy modifiers
    [Export] public float BasePower { get; set; } = DefaultBasePower;
    [Export] public float BaseValue { get; set; } = DefaultBaseValue; // Monetary value
    [Export] public int BaseCost { get; set; } = DefaultBaseCost; // Energy/mana cost

    // Residual energy modifiers - how signature differences affect this card
    [Export] public Array<Services.ResidualEnergyModifier> Modifiers { get; set; } = new();

    // Base effect description template (will be modified by residual energy)
    [Export] public string BaseEffectTemplate { get; set; } = DefaultBaseEffectTemplate;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TypeName) => true,
            nameof(Description) => true,
            nameof(Category) => true,
            nameof(BaseRarity) => true,
            nameof(BaseSignature) => true,
            nameof(MatchRadius) => true,
            nameof(BasePower) => true,
            nameof(BaseValue) => true,
            nameof(BaseCost) => true,
            nameof(BaseEffectTemplate) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TypeName) => DefaultTypeName,
            nameof(Description) => DefaultDescription,
            nameof(Category) => (int)DefaultCategory,
            nameof(BaseRarity) => (int)DefaultBaseRarity,
            nameof(BaseSignature) => Variant.From(DefaultBaseSignature),
            nameof(MatchRadius) => DefaultMatchRadius,
            nameof(BasePower) => DefaultBasePower,
            nameof(BaseValue) => DefaultBaseValue,
            nameof(BaseCost) => DefaultBaseCost,
            nameof(BaseEffectTemplate) => DefaultBaseEffectTemplate,
            _ => base._PropertyGetRevert(property)
        };
    }

    public bool CanMatch(CardSignature signature, float maxDistance = -1f)
    {
        var distance = signature.DistanceTo(BaseSignature);
        var threshold = maxDistance > 0 ? maxDistance : MatchRadius;
        return distance <= threshold;
    }

    public float CalculateMatchWeight(CardSignature signature)
    {
        var distance = signature.DistanceTo(BaseSignature);
        if (distance > MatchRadius) return 0f;

        // Closer signatures get higher weight (inverse distance)
        return 1f / (1f + distance);
    }

    public CardRarity CalculateActualRarity(CardSignature signature)
    {
        // Base rarity can be modified by how unusual the signature is
        var residual = signature.Subtract(BaseSignature);
        var unusualness = CalculateUnusualness(residual);

        var rarityBoost = Mathf.FloorToInt(unusualness * 2f); // 0-2 rarity levels boost
        var newRarity = Math.Min((int)BaseRarity + rarityBoost, (int)CardRarity.Legendary);

        return (CardRarity)newRarity;
    }

    public float CalculateActualValue(CardSignature signature)
    {
        var actualRarity = CalculateActualRarity(signature);
        var rarityMultiplier = (int)actualRarity + 1f; // Common=1x, Legendary=5x

        var residual = signature.Subtract(BaseSignature);
        var powerModifier = CalculatePowerModifier(residual);

        return BaseValue * rarityMultiplier * (1f + powerModifier * 0.5f);
    }

    private static float CalculateUnusualness(CardSignature residual)
    {
        // How far the residual energy deviates from the base
        var totalDeviation = 0f;
        for (var i = 0; i < 8; i++)
            totalDeviation += Mathf.Abs(residual[i]);

        return Mathf.Clamp(totalDeviation / 4f, 0f, 1f); // Normalize to 0-1
    }

    private float CalculatePowerModifier(CardSignature residual)
    {
        var totalMagnitude = 0f;
        foreach (var modifier in Modifiers)
            totalMagnitude += modifier.CalculateEffect(residual);

        return totalMagnitude;
    }
}