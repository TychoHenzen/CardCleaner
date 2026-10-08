using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Services;

/// <summary>
///     What the signature card generator hands the card shader for the art-region effects: the effect ids mapped from
///     rarity and intensity, the seed, and the baked bevel map. Each test reads them back from a real material.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SignatureCardArtEffectsTest
{
    // A Rare card (glow) with a mean element magnitude of 0.15, which is Dormant (worn).
    private static readonly float[] RareDormant = { 0.7f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f };

    private SignatureCardGenerator _generator = null!;

    [BeforeTest]
    public void Setup()
    {
        SignatureCardTestData.Register();
        _generator = new SignatureCardGenerator();
    }

    [AfterTest]
    public static void TearDown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardCarriesTheEffectIdsMappedFromItsRarityAndIntensity()
    {
        var material = GenerateAndApply(new CardSignature(RareDormant));

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.Glow);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void IntenseEpicCardGetsGlossyAndShiny()
    {
        var signature = CardEffectComparisonGrid.SignatureFor(CardRarity.Epic, IntensityTier.Intense);

        var material = GenerateAndApply(signature);

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.Glossy);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Shiny);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardSeedComesFromTheSignatureAlone()
    {
        var signature = new CardSignature(RareDormant);
        var other = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0f, 0f, 0f, 0f, 0f });

        var first = (float)GenerateAndApply(signature).GetShaderParameter("art_seed");
        var again = (float)GenerateAndApply(signature).GetShaderParameter("art_seed");
        var different = (float)GenerateAndApply(other).GetShaderParameter("art_seed");

        AssertThat(first).IsEqual(SignatureCardHelper.EffectSeed(signature));
        AssertThat(again).IsEqual(first);
        AssertThat(different).IsNotEqual(first);
        AssertThat(first).IsLess(10000f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardWithRarityEffectCarriesTheBakedNormalMapOfItsArt()
    {
        var template = new CardTemplate();

        var material = GenerateAndApply(new CardSignature(RareDormant), template);
        var normalMap = material.GetShaderParameter("art_normal_map").As<Texture2D>();

        AssertThat(template.Art.Texture).IsNotNull();
        AssertThat(normalMap).IsNotNull();
        AssertThat(normalMap).IsSame(CardEffectNormalMapCache.GetOrBake(template.Art.Texture!));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CommonCardHasNoRarityEffectButStillLooksWorn()
    {
        // Intensity is the mean element magnitude, so the all-zero signature of an ordinary pack card is Dormant
        // and nearly every common card shows the worn scratches. The owner decides later whether that stays.
        var material = GenerateAndApply(new CardSignature());

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
        AssertThat(material.GetShaderParameter("art_normal_map").Obj).IsNull();
    }

    /// <summary>Generates a card on a real renderer and returns the material it ends up with.</summary>
    private ShaderMaterial GenerateAndApply(CardSignature signature, CardTemplate? template = null)
    {
        var renderer = new CardShaderRenderer();
        var manager = new CardMaterialManager
        {
            Name = "MaterialManager",
            CardMaterialTemplate = GD.Load<ShaderMaterial>("res://Assets/Materials/CardMaterial.tres")
        };
        renderer.AddChild(manager);
        renderer.NameLabel = new Label3D();
        renderer.AddChild(renderer.NameLabel);
        renderer.AttrLabel = new Label3D();
        renderer.AddChild(renderer.AttrLabel);
        Assertions.AddNode(renderer);
        renderer.Setup(renderer);

        _generator.GenerateCardRenderer(renderer, signature, template ?? new CardTemplate());
        return manager.ApplyMaterial(Assertions.AddNode(new MeshInstance3D()))
               ?? throw new System.InvalidOperationException("No material applied");
    }
}
