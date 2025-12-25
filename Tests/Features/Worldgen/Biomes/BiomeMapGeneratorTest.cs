using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public class BiomeMapGeneratorTest
{
    private BiomeRegistry _registry = null!;
    private RandomNumberGenerator _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _registry = new BiomeRegistry();
        _registry.RegisterDefaultBiomes();
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
    }

    [TestCase]
    public void TestGetBiomeAtReturnsValidBiome()
    {
        var gradient = new CardBasedGradient(new[] { new CardSignature() }, _rng);
        var mapSize = new Vector2I(20, 20);
        var generator = new BiomeMapGenerator(_registry, gradient, mapSize);

        var biome = generator.GetBiomeAt(new Vector2I(10, 10));

        AssertThat(biome).IsNotNull();
    }

    [TestCase]
    public void TestGetSignatureAtReturnsSignature()
    {
        var inputSignature = new CardSignature(new[] { 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f });
        var gradient = new CardBasedGradient(new[] { inputSignature }, _rng);
        var mapSize = new Vector2I(20, 20);
        var generator = new BiomeMapGenerator(_registry, gradient, mapSize);

        var signature = generator.GetSignatureAt(new Vector2I(10, 10));

        AssertThat(signature).IsNotNull();
    }

    [TestCase]
    public void TestHotSignatureReturnsDesert()
    {
        var hotSignature = new CardSignature(new[] { 0.3f, 0.8f, 0.3f, 0.2f, 0f, -0.2f, -0.1f, 0.2f });
        var gradient = new ConstantSignatureGradient(hotSignature);
        var mapSize = new Vector2I(20, 20);
        var generator = new BiomeMapGenerator(_registry, gradient, mapSize);

        var biome = generator.GetBiomeAt(new Vector2I(10, 10));

        AssertThat(biome.Type).IsEqual(BiomeType.Desert);
    }

    [TestCase]
    public void TestColdSignatureReturnsTundra()
    {
        var coldSignature = new CardSignature(new[] { 0.1f, -0.8f, 0.4f, 0.3f, 0f, 0.1f, 0f, -0.1f });
        var gradient = new ConstantSignatureGradient(coldSignature);
        var mapSize = new Vector2I(20, 20);
        var generator = new BiomeMapGenerator(_registry, gradient, mapSize);

        var biome = generator.GetBiomeAt(new Vector2I(10, 10));

        AssertThat(biome.Type).IsEqual(BiomeType.Tundra);
    }

    [TestCase]
    public void TestCoolSignatureReturnsForest()
    {
        var forestSignature = new CardSignature(new[] { 0f, -0.4f, -0.2f, -0.2f, 0f, 0.2f, 0.3f, 0f });
        var gradient = new ConstantSignatureGradient(forestSignature);
        var mapSize = new Vector2I(20, 20);
        var generator = new BiomeMapGenerator(_registry, gradient, mapSize);

        var biome = generator.GetBiomeAt(new Vector2I(10, 10));

        AssertThat(biome.Type).IsEqual(BiomeType.Forest);
    }

    [TestCase]
    public void TestDifferentPositionsCanReturnDifferentBiomes()
    {
        var signature1 = new CardSignature(new[] { 0f, -0.6f, 0f, 0f, 0f, 0f, 0f, 0f });
        var signature2 = new CardSignature(new[] { 0f, 0.6f, 0f, 0f, 0f, 0f, 0f, 0f });
        var gradient = new TwoZoneGradient(signature1, signature2);
        var mapSize = new Vector2I(20, 20);
        var generator = new BiomeMapGenerator(_registry, gradient, mapSize);

        var biomeLeft = generator.GetBiomeAt(new Vector2I(0, 10));
        var biomeRight = generator.GetBiomeAt(new Vector2I(19, 10));

        AssertBool(biomeLeft.Type != biomeRight.Type).IsTrue();
    }

    [TestCase]
    public void TestWithEmptyRegistryReturnsFallback()
    {
        var emptyRegistry = new BiomeRegistry();
        var gradient = new CardBasedGradient(new[] { new CardSignature() }, _rng);
        var mapSize = new Vector2I(20, 20);
        var generator = new BiomeMapGenerator(emptyRegistry, gradient, mapSize);

        var biome = generator.GetBiomeAt(new Vector2I(10, 10));

        AssertThat(biome).IsNotNull();
        AssertThat(biome.Type).IsEqual(BiomeType.Plains);
    }
}

internal sealed partial class ConstantSignatureGradient : BaselineGradient
{
    private readonly CardSignature _signature;

    public ConstantSignatureGradient(CardSignature signature)
    {
        _signature = signature;
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize) => _signature;
}

internal sealed partial class TwoZoneGradient : BaselineGradient
{
    private readonly CardSignature _left;
    private readonly CardSignature _right;

    public TwoZoneGradient(CardSignature left, CardSignature right)
    {
        _left = left;
        _right = right;
    }

    public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize) =>
        position.X < mapSize.X / 2 ? _left : _right;
}
