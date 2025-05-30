using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;
using ResidualEnergyModifier = CardCleaner.Scripts.Features.Card.Services.ResidualEnergyModifier;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class ResidualEnergyModifierTest
{
    [TestCase]
    public void TestPositiveElementEffect()
    {
        var modifier = new ResidualEnergyModifier();
        modifier.SourceElement = Element.Solidum;
        modifier.UsePositiveAspect = true;
        modifier.Intensity = 2.0f;
        modifier.BaseValue = 1.0f;

        var signature = new CardSignature();
        signature.Solidum = 0.5f; // Positive value

        var effect = modifier.CalculateEffect(signature);
        Assertions.AssertFloat(effect).IsEqual(2.0f); // 1.0 + (0.5 * 2.0)
    }

    [TestCase]
    public void TestNegativeElementEffect()
    {
        var modifier = new ResidualEnergyModifier();
        modifier.SourceElement = Element.Solidum;
        modifier.UsePositiveAspect = false; // Want negative aspect
        modifier.Intensity = 2.0f;
        modifier.BaseValue = 1.0f;

        var signature = new CardSignature();
        signature.Solidum = -0.5f; // Negative value

        var effect = modifier.CalculateEffect(signature);
        Assertions.AssertFloat(effect).IsEqual(2.0f); // 1.0 + (0.5 * 2.0) after flipping
    }

    [TestCase]
    public void TestWrongAspectGivesBaseValue()
    {
        var modifier = new ResidualEnergyModifier();
        modifier.SourceElement = Element.Solidum;
        modifier.UsePositiveAspect = true; // Want positive
        modifier.Intensity = 2.0f;
        modifier.BaseValue = 1.0f;

        var signature = new CardSignature();
        signature.Solidum = -0.5f; // But signature is negative

        var effect = modifier.CalculateEffect(signature);
        Assertions.AssertFloat(effect).IsEqual(1.0f); // Should return base value only
    }

    [TestCase]
    public void TestCreatePowerModifier()
    {
        var modifier = ResidualEnergyModifier.CreatePowerModifier(Element.Febris, true, 1.5f);

        Assertions.AssertThat(modifier.Type).IsEqual(ModifierType.Power);
        Assertions.AssertThat(modifier.SourceElement).IsEqual(Element.Febris);
        Assertions.AssertBool(modifier.UsePositiveAspect).IsTrue();
        Assertions.AssertFloat(modifier.Intensity).IsEqual(1.5f);
        Assertions.AssertFloat(modifier.BaseValue).IsEqual(0.0f);
    }

    [TestCase]
    public void TestCreateCostModifier()
    {
        var modifier = ResidualEnergyModifier.CreateCostModifier(Element.Febris);

        Assertions.AssertThat(modifier.Type).IsEqual(ModifierType.Cost);
        Assertions.AssertThat(modifier.SourceElement).IsEqual(Element.Febris);
        Assertions.AssertFloat(modifier.Intensity).IsEqual(-0.5f); // Should be negative
        Assertions.AssertFloat(modifier.BaseValue).IsEqual(1.0f);
    }

    [TestCase]
    public void TestGetRelevantAspect()
    {
        var positiveModifier = new ResidualEnergyModifier();
        positiveModifier.SourceElement = Element.Solidum;
        positiveModifier.UsePositiveAspect = true;

        var negativeModifier = new ResidualEnergyModifier();
        negativeModifier.SourceElement = Element.Solidum;
        negativeModifier.UsePositiveAspect = false;

        Assertions.AssertThat(positiveModifier.GetRelevantAspect()).IsEqual(Aspect.Tellus);
        Assertions.AssertThat(negativeModifier.GetRelevantAspect()).IsEqual(Aspect.Aeolis);
    }
}