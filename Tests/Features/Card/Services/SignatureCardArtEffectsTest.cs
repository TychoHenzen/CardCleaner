using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Tests.Features.Card.Services;

/// <summary>
///     What the signature card generator hands the card shader for the art-region effects: the effect ids mapped from
///     rarity and intensity, the seed, and the baked bevel map. Each test reads them back from a real material. The
///     bevel map is baked off the main thread, so a card shows its rarity effect once the map has arrived.
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
        CardEffectNormalMapCache.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CardCarriesTheEffectIdsMappedFromItsRarityAndIntensity()
    {
        var material = await GenerateAndApplyBaked(new CardSignature(RareDormant));

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.Glow);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task IntenseEpicCardGetsGlossyAndShiny()
    {
        var signature = CardEffectComparisonGrid.SignatureFor(CardRarity.Epic, IntensityTier.Intense);

        var material = await GenerateAndApplyBaked(signature);

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

        AssertThat(first).IsEqual(CardEffectSeed.For(signature));
        AssertThat(again).IsEqual(first);
        AssertThat(different).IsNotEqual(first);
        AssertThat(first).IsLess(10000f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CardShowsNoRarityEffectUntilTheBevelMapOfItsArtIsBaked()
    {
        var template = new CardTemplate();

        // The art texture is new, so its bevel map is not baked yet when the card is generated.
        var material = GenerateAndApply(new CardSignature(RareDormant), template);

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
        AssertThat(material.GetShaderParameter("art_normal_map").Obj).IsNull();
        await CardEffectBakeWait.Until(
            () => (int)material.GetShaderParameter("rarity_effect") == (int)RarityEffect.Glow, "the glow effect");
        AssertThat(material.GetShaderParameter("art_normal_map").As<Texture2D>())
            .IsSame(await CardEffectBakeWait.BakeOf(template.Art.Texture!));
        AssertThat((float)material.GetShaderParameter("art_seed")).IsEqual(CardEffectSeed.For(new CardSignature(RareDormant)));
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CardWhoseArtIsAlreadyBakedShowsItsRarityEffectAtOnce()
    {
        var first = new CardTemplate();
        var firstMaterial = GenerateAndApply(new CardSignature(RareDormant), first);
        await CardEffectBakeWait.Until(() => firstMaterial.GetShaderParameter("art_normal_map").Obj != null,
            "the first card's bevel map");
        var second = new CardTemplate();

        var material = GenerateAndApply(new CardSignature(RareDormant), second);

        AssertThat(second.Art.Texture).IsSame(first.Art.Texture);
        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.Glow);
        AssertThat(material.GetShaderParameter("art_normal_map").As<Texture2D>())
            .IsSame(firstMaterial.GetShaderParameter("art_normal_map").As<Texture2D>());
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardFreedBeforeItsBevelMapIsBakedDoesNotStopOtherCardsGettingIt()
    {
        var template = new CardTemplate();
        var freed = CreateRenderer();
        _generator.GenerateCardRenderer(freed, new CardSignature(RareDormant), template);
        freed.Free();

        // The freed card's request comes first, so a throw on it would keep this one from ever arriving.
        var map = await CardEffectBakeWait.BakeOf(template.Art.Texture!);

        AssertThat(map).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task GeneratingACardAgainBeforeItsBevelMapArrivesKeepsTheNewerEffects()
    {
        var renderer = Assertions.AddNode(CreateRenderer());
        var first = new CardTemplate();
        _generator.GenerateCardRenderer(renderer, new CardSignature(RareDormant), first);
        var firstArt = first.Art.Texture!;

        // A common card has no rarity effect, so generating it supersedes the first card's pending bevel map.
        var common = new CardSignature();
        _generator.GenerateCardRenderer(renderer, common, new CardTemplate());
        var material = renderer.GetNode<CardMaterialManager>("MaterialManager")
            .ApplyMaterial(Assertions.AddNode(new MeshInstance3D()))!;
        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);

        // The first card's bevel map arrives now and must not change the effects of the second card.
        await CardEffectBakeWait.BakeOf(firstArt);

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        AssertThat(material.GetShaderParameter("art_normal_map").Obj).IsNull();
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
        AssertThat((float)material.GetShaderParameter("art_seed")).IsEqual(CardEffectSeed.For(common));
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ABevelMapArrivingWhileTheEffectsAreOffWaitsUntilTheyAreSwitchedOn()
    {
        var renderer = Assertions.AddNode(CreateRenderer());
        var template = new CardTemplate();
        var signature = new CardSignature(RareDormant);
        _generator.GenerateCardRenderer(renderer, signature, template);
        var material = renderer.GetNode<CardMaterialManager>("MaterialManager")
            .ApplyMaterial(Assertions.AddNode(new MeshInstance3D()))!;
        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);

        renderer.ArtEffectsEnabled = false;
        var map = await CardEffectBakeWait.BakeOf(template.Art.Texture!);

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.None);

        renderer.ArtEffectsEnabled = true;

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.Glow);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
        AssertThat(material.GetShaderParameter("art_normal_map").As<Texture2D>()).IsSame(map);
        AssertThat((float)material.GetShaderParameter("art_seed")).IsEqual(CardEffectSeed.For(signature));
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

    /// <summary>Generates a card with a rarity effect and waits until that effect has reached the material.</summary>
    private async Task<ShaderMaterial> GenerateAndApplyBaked(CardSignature signature)
    {
        var material = GenerateAndApply(signature);
        await CardEffectBakeWait.Until(() => (int)material.GetShaderParameter("rarity_effect") != (int)RarityEffect.None,
            "the card's rarity effect");
        return material;
    }

    /// <summary>Generates a card on a real renderer and returns the material it ends up with.</summary>
    private ShaderMaterial GenerateAndApply(CardSignature signature, CardTemplate? template = null)
    {
        var renderer = Assertions.AddNode(CreateRenderer());
        _generator.GenerateCardRenderer(renderer, signature, template ?? new CardTemplate());
        return renderer.GetNode<CardMaterialManager>("MaterialManager")
                   .ApplyMaterial(Assertions.AddNode(new MeshInstance3D()))
               ?? throw new System.InvalidOperationException("No material applied");
    }

    /// <summary>A real renderer with its material manager and labels, not yet in the scene tree.</summary>
    private static CardShaderRenderer CreateRenderer()
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
        renderer.Setup(renderer);
        return renderer;
    }
}
