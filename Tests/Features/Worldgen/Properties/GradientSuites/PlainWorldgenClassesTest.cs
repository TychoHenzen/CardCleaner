using System;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties.GradientSuites;

/// <summary>
///     Pins that the worldgen definitions and gradients are plain C# classes with the defaults the inspector used to show.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlainWorldgenClassesTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void NoneOfTheSevenClassesIsAGodotObject()
    {
        Type[] types =
        [
            typeof(BiomeDefinition), typeof(TilePool), typeof(TilePoolEntry), typeof(BaselineGradient),
            typeof(CardBasedGradient), typeof(RadialGradient), typeof(NoiseGradient)
        ];
        foreach (var type in types)
            AssertBool(typeof(GodotObject).IsAssignableFrom(type)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DefaultsMatchTheValuesTheInspectorRevertUsedToReturn()
    {
        var noise = new NoiseGradient(GodotNoiseSource.Create);
        AssertFloat(noise.NoiseScale).IsEqual(0.1f);
        AssertFloat(noise.NoiseStrength).IsEqual(0.3f);
        AssertThat(noise.NoiseSeed).IsEqual(42);
        AssertFloat(new RadialGradient().Falloff).IsEqual(1.0f);
        AssertThat(new BiomeDefinition().Id).IsEqual("plains");
        AssertFloat(new BiomeDefinition().BlockedPercentage).IsEqual(0.3f);
        AssertThat(new TilePoolEntry().TileId).IsEqual("");
        AssertFloat(new TilePoolEntry().Weight).IsEqual(1.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BiomeDefinitionConstructorClampsTheBlockedPercentage()
    {
        var high = new BiomeDefinition("a", new CardSignature(), new TilePool(), new TilePool(), 1.7f);
        var low = new BiomeDefinition("b", new CardSignature(), new TilePool(), new TilePool(), -2f);
        AssertFloat(high.BlockedPercentage).IsEqual(1f);
        AssertFloat(low.BlockedPercentage).IsEqual(0f);
    }
}
