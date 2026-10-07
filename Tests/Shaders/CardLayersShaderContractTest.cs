using System;
using System.Text.RegularExpressions;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

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
        var guard = Source().Contains("if (i == ART_LAYER_INDEX && isFront && art_effects_on) {");

        AssertThat(applications.Count).IsEqual(1);
        AssertThat(guard).IsTrue();
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
