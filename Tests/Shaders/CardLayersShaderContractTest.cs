using System;
using System.Text.RegularExpressions;
using CardCleaner.Scripts.Core.Data;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Tests.Shaders;

/// <summary>
///     The shader and the C# side share numbers: effect ids and the Art layer's index. Headless Godot does not
///     compile shaders, so these tests read the shader source to keep the two in step.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardLayersShaderContractTest
{
    private const string ShaderPath = "res://Shaders/card_layers.gdshader";

    private static string Source()
    {
        return Godot.FileAccess.GetFileAsString(ShaderPath);
    }

    private static int ShaderConstant(string name)
    {
        var match = Regex.Match(Source(), $@"^const int {name} = (\d+);", RegexOptions.Multiline);
        AssertThat(match.Success).OverrideFailureMessage($"{ShaderPath} declares no `const int {name}`").IsTrue();
        return int.Parse(match.Groups[1].Value);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryRarityEffectHasItsIdInTheShader()
    {
        foreach (var effect in Enum.GetValues<RarityEffect>())
            AssertThat(ShaderConstant("RARITY_" + effect.ToString().ToUpperInvariant())).IsEqual((int)effect);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryConditionEffectHasItsIdInTheShader()
    {
        foreach (var effect in Enum.GetValues<ConditionEffect>())
            AssertThat(ShaderConstant("CONDITION_" + effect.ToString().ToUpperInvariant())).IsEqual((int)effect);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheShaderFindsTheArtLayerWhereTheTemplateKeepsIt()
    {
        var template = new CardTemplate();

        var index = Array.IndexOf(template.GatherAllLayers(), template.Art);

        AssertThat(ShaderConstant("ART_LAYER_INDEX")).IsEqual(index);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheEffectUniformsDefaultToOffSoExistingCardsRenderUnchanged()
    {
        var source = Source();

        AssertThat(Regex.IsMatch(source, @"uniform int rarity_effect[^;]*= 0;")).IsTrue();
        AssertThat(Regex.IsMatch(source, @"uniform int condition_effect[^;]*= 0;")).IsTrue();
        AssertThat(Regex.IsMatch(source, @"uniform float art_seed = 0\.0;")).IsTrue();
        AssertThat(Regex.IsMatch(source, @"uniform sampler2D art_normal_map[^;]*hint_default_transparent;")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EachOfTheSixEffectsIsOneFunctionInTheShader()
    {
        var source = Source();

        foreach (var name in new[] { "embossed", "glow", "glossy", "foil", "worn", "shiny" })
            AssertThat(Regex.IsMatch(source, $@"^vec4 {name}_effect\(", RegexOptions.Multiline))
                .OverrideFailureMessage($"{ShaderPath} has no vec4 {name}_effect(...)")
                .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EffectsAreOnlyAppliedToTheArtLayerOfTheFront()
    {
        var applications = Regex.Matches(Source(), @"art_with_effects\(sample\.rgb");
        var guard = Source().Contains("if (i == ART_LAYER_INDEX && isFront && art_effects_on)");

        AssertThat(applications.Count).IsEqual(1);
        AssertThat(guard).IsTrue();
    }

    /// <remarks>
    ///     The call passes <c>sampleUV</c>, which the layer loop declares inside the art region's bounds check, so
    ///     the call cannot sit outside the region. Changing only the colour keeps the art's own alpha, so an effect
    ///     never makes a transparent part of the art, or the card around it, visible.
    /// </remarks>
    [TestCase]
    [TestCategory("Unit")]
    public static void EffectsRecolourTheArtButNeverChangeWhereItIsVisible()
    {
        var source = Source();

        AssertThat(Regex.IsMatch(source, @"^vec3 art_with_effects\(", RegexOptions.Multiline)).IsTrue();
        AssertThat(Regex.Matches(source, @"sample\.rgb = art_with_effects\(sample\.rgb, sampleUV, ").Count)
            .IsEqual(1);
        AssertThat(Regex.IsMatch(source, @"sample(\.a)? = art_with_effects")).IsFalse();
        AssertThat(Regex.IsMatch(source, @"sample\.a\s*[-+*/]?=")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheBevelMapDoesNotWrapAroundTheArtEdges()
    {
        // A repeating, linearly filtered sampler blends the far edge's bevel into the near edge.
        AssertThat(Regex.IsMatch(Source(), @"uniform sampler2D art_normal_map[^;]*\brepeat_disable\b"))
            .OverrideFailureMessage($"{ShaderPath}: art_normal_map must be declared repeat_disable")
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheScratchLayoutReadsNothingButTheCardSeedAndThePositionInTheArt()
    {
        var source = Source();

        AssertThat(Regex.IsMatch(source, @"^vec4 worn_effect\(vec2 art_uv, float seed\)", RegexOptions.Multiline))
            .OverrideFailureMessage($"{ShaderPath}: worn_effect must take only the art position and the seed")
            .IsTrue();
        // One declaration and one call, and the call hands it the per-card seed.
        AssertThat(Regex.Matches(source, @"\bworn_effect\(").Count).IsEqual(2);
        AssertThat(source.Contains("worn_effect(art_uv, art_seed)")).IsTrue();
        // Nothing in the shader animates, so a card that does not move renders the same every frame.
        AssertThat(Regex.IsMatch(source, @"\bTIME\b")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheArtRegionTheShaderMasksWithMatchesTheTemplate()
    {
        var template = new CardTemplate();

        AssertThat(template.Art.Region).IsEqual(new Rect2(0.1f, 0.1f, 0.8f, 0.35f));
        AssertThat(template.Art.RenderOnFront).IsTrue();
        AssertThat(template.Art.RenderOnBack).IsFalse();
    }
}
