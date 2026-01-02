using System.Diagnostics;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public partial class BiomeStrengthGridTest
{
    private BiomeRegistry _registry = null!;
    private TestGradient _gradient = null!;

    [BeforeTest]
    public void Setup()
    {
        _registry = new BiomeRegistry();
        _gradient = new TestGradient();

        // Create test biomes with distinct signatures
        var fireSignature = new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }); // High Febris (fire)
        var waterSignature = new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f }); // Low Febris (water)
        var neutralSignature = new CardSignature(); // All zeros

        var fireBiome = new BiomeDefinition("fire", fireSignature, new TilePool(), new TilePool());
        var waterBiome = new BiomeDefinition("water", waterSignature, new TilePool(), new TilePool());
        var neutralBiome = new BiomeDefinition("neutral", neutralSignature, new TilePool(), new TilePool());

        _registry.Register(fireBiome);
        _registry.Register(waterBiome);
        _registry.Register(neutralBiome);
    }

    [TestCase]
    public void GetStrength_ValidPosition_ReturnsValueInRange()
    {
        // Arrange: Gradient returns a signature close to fire biome at (0,0)
        _gradient.SignatureToReturn = new CardSignature(new[] { 0f, 0.8f, 0f, 0f, 0f, 0f, 0f, 0f });

        var grid = new BiomeStrengthGrid(new Vector2I(10, 10), _gradient, _registry);

        // Act
        var strength = grid.GetStrength(new Vector2I(0, 0), "fire");

        // Assert: Strength should be in [-1, 1] range
        AssertFloat(strength).IsBetween(-1f, 1f);
        // Should be positive since position is close to fire biome
        AssertFloat(strength).IsGreater(0f);
    }

    [TestCase]
    public void GetStrength_UnknownBiome_ReturnsZero()
    {
        // Arrange
        _gradient.SignatureToReturn = new CardSignature();
        var grid = new BiomeStrengthGrid(new Vector2I(10, 10), _gradient, _registry);

        // Act
        var strength = grid.GetStrength(new Vector2I(5, 5), "nonexistent_biome");

        // Assert
        AssertFloat(strength).IsEqual(0f);
    }

    [TestCase]
    public void GetStrength_OutOfBoundsPosition_ReturnsZero()
    {
        // Arrange
        _gradient.SignatureToReturn = new CardSignature();
        var grid = new BiomeStrengthGrid(new Vector2I(10, 10), _gradient, _registry);

        // Act & Assert
        AssertFloat(grid.GetStrength(new Vector2I(-1, 0), "fire")).IsEqual(0f);
        AssertFloat(grid.GetStrength(new Vector2I(0, -1), "fire")).IsEqual(0f);
        AssertFloat(grid.GetStrength(new Vector2I(10, 0), "fire")).IsEqual(0f);
        AssertFloat(grid.GetStrength(new Vector2I(0, 10), "fire")).IsEqual(0f);
    }

    [TestCase]
    public void Computation_75x75Grid_CompletesUnder100ms()
    {
        // Arrange: Set up larger registry for realistic test
        var largeRegistry = new BiomeRegistry();
        for (var i = 0; i < 5; i++)
        {
            var signature = new CardSignature(new[] { i * 0.2f, 0f, 0f, 0f, 0f, 0f, 0f, 0f });
            largeRegistry.Register(new BiomeDefinition($"biome_{i}", signature, new TilePool(), new TilePool()));
        }

        _gradient.SignatureToReturn = new CardSignature();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var grid = new BiomeStrengthGrid(new Vector2I(75, 75), _gradient, largeRegistry);
        stopwatch.Stop();

        // Assert
        AssertThat(stopwatch.ElapsedMilliseconds).IsLess(100);
        AssertThat(grid.Size).IsEqual(new Vector2I(75, 75));
        AssertThat(grid.BiomeCount).IsEqual(5);
    }

    [TestCase]
    public void GetStrength_IdenticalSignatures_ReturnsPositiveOne()
    {
        // Arrange: Gradient returns exactly the fire biome's signature
        _gradient.SignatureToReturn = new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f });
        var grid = new BiomeStrengthGrid(new Vector2I(10, 10), _gradient, _registry);

        // Act
        var strength = grid.GetStrength(new Vector2I(0, 0), "fire");

        // Assert: Perfect match should give strength of 1.0
        AssertFloat(strength).IsEqual(1f);
    }

    [TestCase]
    public void GetStrength_MaxDistantSignatures_ReturnsNegative()
    {
        // Arrange: Gradient returns opposite of fire biome (water-like)
        _gradient.SignatureToReturn = new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f });
        var grid = new BiomeStrengthGrid(new Vector2I(10, 10), _gradient, _registry);

        // Act
        var strength = grid.GetStrength(new Vector2I(0, 0), "fire");

        // Assert: Opposite signature should give negative strength
        AssertFloat(strength).IsLess(0f);
    }

    /// <summary>
    /// Test gradient that returns a configurable signature for all positions.
    /// </summary>
    private sealed partial class TestGradient : BaselineGradient
    {
        public CardSignature SignatureToReturn { get; set; } = new();

        public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
        {
            return SignatureToReturn;
        }
    }
}
