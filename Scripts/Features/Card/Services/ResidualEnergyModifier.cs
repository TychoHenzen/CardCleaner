using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Utilities;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

public enum ModifierType
{
    Power, // Affects damage/effectiveness
    Cost, // Affects energy/mana cost
    Duration, // Affects how long effects last
    Range, // Affects area of effect/targeting range
    Healing, // Adds healing effects
    Speed, // Affects attack speed or movement
    Defense, // Adds defensive properties
    Special // Custom effect modifications
}

[Tool]
[GlobalClass]
public partial class ResidualEnergyModifier : Resource
{
    // Default values as constants
    private const string DefaultName = "";
    private const ModifierType DefaultType = ModifierType.Power;
    private const Element DefaultSourceElement = Element.Solidum;
    private const float DefaultIntensity = 1.0f;
    private const bool DefaultUsePositiveAspect = true;
    private const float DefaultBaseValue = 0f;
    private const string DefaultEffectTemplate = "";

    [Export] public string Name { get; set; } = DefaultName;
    [Export] public ModifierType Type { get; set; } = DefaultType;
    [Export] public Element SourceElement { get; set; } = DefaultSourceElement;
    [Export] public float Intensity { get; set; } = DefaultIntensity; // How strongly this element affects the modifier
    [Export] public bool UsePositiveAspect { get; set; } = DefaultUsePositiveAspect; // Whether to use positive or negative aspect
    [Export] public float BaseValue { get; set; } = DefaultBaseValue; // Starting value before residual energy
    [Export] public string EffectTemplate { get; set; } = DefaultEffectTemplate; // Text template for describing the effect

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Name) => true,
            nameof(Type) => true,
            nameof(SourceElement) => true,
            nameof(Intensity) => true,
            nameof(UsePositiveAspect) => true,
            nameof(BaseValue) => true,
            nameof(EffectTemplate) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Name) => DefaultName,
            nameof(Type) => (int)DefaultType,
            nameof(SourceElement) => (int)DefaultSourceElement,
            nameof(Intensity) => DefaultIntensity,
            nameof(UsePositiveAspect) => DefaultUsePositiveAspect,
            nameof(BaseValue) => DefaultBaseValue,
            nameof(EffectTemplate) => DefaultEffectTemplate,
            _ => base._PropertyGetRevert(property)
        };
    }

    public float CalculateEffect(Models.CardSignature residualEnergy)
    {
        var elementValue = residualEnergy[SourceElement];

        // If we want negative aspect, flip the value
        if (!UsePositiveAspect)
            elementValue = -elementValue;

        // Only apply if the element is aligned with our desired aspect
        if (elementValue < 0) return BaseValue;

        return BaseValue + elementValue * Intensity;
    }

    public string GenerateEffectText(Models.CardSignature residualEnergy)
    {
        var effect = CalculateEffect(residualEnergy);

        if (string.IsNullOrEmpty(EffectTemplate))
            return $"{Type}: {effect:F1}";

        // Replace placeholders in template
        return EffectTemplate
            .Replace("{VALUE}", effect.ToString("F1"))
            .Replace("{TYPE}", Type.ToString())
            .Replace("{ELEMENT}", SourceElement.ToString());
    }

    public Aspect GetRelevantAspect()
    {
        return UsePositiveAspect ? SourceElement.Positive() : SourceElement.Negative();
    }

    public static ResidualEnergyModifier CreatePowerModifier(Element element, bool positive = true,
        float intensity = 1.0f)
    {
        return new ResidualEnergyModifier
        {
            Name = $"{(positive ? element.Positive() : element.Negative())} Power",
            Type = ModifierType.Power,
            SourceElement = element,
            UsePositiveAspect = positive,
            Intensity = intensity,
            BaseValue = 0f,
            EffectTemplate = "Power +{VALUE}"
        };
    }

    public static ResidualEnergyModifier CreateCostModifier(Element element, bool positive = true,
        float intensity = 0.5f)
    {
        return new ResidualEnergyModifier
        {
            Name = $"{(positive ? element.Positive() : element.Negative())} Efficiency",
            Type = ModifierType.Cost,
            SourceElement = element,
            UsePositiveAspect = positive,
            Intensity = -intensity, // Negative because more element = less cost
            BaseValue = 1f,
            EffectTemplate = "Cost {VALUE}"
        };
    }
}