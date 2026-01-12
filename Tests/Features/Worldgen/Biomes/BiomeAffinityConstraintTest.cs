using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

[TestSuite]
[RequireGodotRuntime]
public partial class BiomeAffinityConstraintTest
{
    private BiomeRegistry _registry = null!;
    private TileRegistry _tileRegistry = null!;
    private WfcGrid _grid = null!;

    [BeforeTest]
    public void Setup()
    {
        _registry = new BiomeRegistry();
        _grid = new WfcGrid(10, 10, new[] { "fire_tile", "water_tile", "neutral_tile" });

        // Create tile registry with tiles that have AllowedBiomes set
        _tileRegistry = new TileRegistry();
        _tileRegistry.RegisterTile(new TileDefinition(
            id: "fire_tile",
            name: "Fire Tile",
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero,
            allowedBiomes: new HashSet<string> { "fire" }
        ));
        _tileRegistry.RegisterTile(new TileDefinition(
            id: "water_tile",
            name: "Water Tile",
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero,
            allowedBiomes: new HashSet<string> { "water" }
        ));
        // neutral_tile has no AllowedBiomes (universal/neutral)
        _tileRegistry.RegisterTile(new TileDefinition(
            id: "neutral_tile",
            name: "Neutral Tile",
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero
        ));

        // Create test biomes with tiles
        var fireSignature = new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f });
        var waterSignature = new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f });

        var firePool = new TilePool();
        firePool.Add("fire_tile");

        var waterPool = new TilePool();
        waterPool.Add("water_tile");

        var emptyPool = new TilePool();

        _registry.Register(new BiomeDefinition("fire", fireSignature, firePool, emptyPool));
        _registry.Register(new BiomeDefinition("water", waterSignature, waterPool, emptyPool));
    }

    [TestCase]
    public void BiomeAffinityConstraint_PositiveBiome_ReturnsBoost()
    {
        // Arrange: Create gradient that returns fire-like signature (high strength for fire biome)
        var gradient = new FixedGradient(new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }));
        var strengthGrid = new BiomeStrengthGrid(new Vector2I(10, 10), gradient, _registry);
        var constraint = new BiomeAffinityConstraint(strengthGrid, _registry, _tileRegistry);

        var context = new WfcConstraintContext
        {
            Position = new Vector2I(5, 5),
            TileId = "fire_tile", // Belongs to fire biome
            Grid = _grid
        };

        // Act
        var modifier = constraint.GetProbabilityModifier(context);

        // Assert: Should boost since fire biome has high strength at this position
        AssertFloat(modifier).IsGreater(1.0f);
    }

    [TestCase]
    public void BiomeAffinityConstraint_NegativeBiome_ReturnsPenalty()
    {
        // Arrange: Create gradient that returns water-like signature (negative strength for fire biome)
        var gradient = new FixedGradient(new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f }));
        var strengthGrid = new BiomeStrengthGrid(new Vector2I(10, 10), gradient, _registry);
        var constraint = new BiomeAffinityConstraint(strengthGrid, _registry, _tileRegistry);

        var context = new WfcConstraintContext
        {
            Position = new Vector2I(5, 5),
            TileId = "fire_tile", // Belongs to fire biome, but biome has negative strength here
            Grid = _grid
        };

        // Act
        var modifier = constraint.GetProbabilityModifier(context);

        // Assert: Should penalize since fire biome has negative strength at water-like position
        AssertFloat(modifier).IsLess(1.0f);
    }

    [TestCase]
    public void BiomeAffinityConstraint_NeutralBiome_ReturnsOne()
    {
        // Arrange: Create gradient that returns neutral signature
        var gradient = new FixedGradient(new CardSignature()); // All zeros
        var strengthGrid = new BiomeStrengthGrid(new Vector2I(10, 10), gradient, _registry);
        var constraint = new BiomeAffinityConstraint(strengthGrid, _registry, _tileRegistry);

        var context = new WfcConstraintContext
        {
            Position = new Vector2I(5, 5),
            TileId = "neutral_tile", // Not in any biome
            Grid = _grid
        };

        // Act
        var modifier = constraint.GetProbabilityModifier(context);

        // Assert: Should be neutral since tile doesn't belong to any biome
        AssertFloat(modifier).IsEqual(1.0f);
    }

    [TestCase]
    public void BiomeAffinityConstraint_TileNotInAnyBiome_ReturnsOne()
    {
        // Arrange
        var gradient = new FixedGradient(new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }));
        var strengthGrid = new BiomeStrengthGrid(new Vector2I(10, 10), gradient, _registry);
        var constraint = new BiomeAffinityConstraint(strengthGrid, _registry, _tileRegistry);

        var context = new WfcConstraintContext
        {
            Position = new Vector2I(5, 5),
            TileId = "unknown_tile", // Not registered in any biome
            Grid = _grid
        };

        // Act
        var modifier = constraint.GetProbabilityModifier(context);

        // Assert: Tiles not in any biome should get neutral modifier
        AssertFloat(modifier).IsEqual(1.0f);
    }

    [TestCase]
    public void BiomeAffinityConstraint_ModifierNeverBelowMinimum()
    {
        // Arrange: Create extremely negative conditions
        var gradient = new FixedGradient(new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f }));
        var strengthGrid = new BiomeStrengthGrid(new Vector2I(10, 10), gradient, _registry);
        var constraint = new BiomeAffinityConstraint(strengthGrid, _registry, _tileRegistry)
        {
            BoostFactor = 2.0f, // Higher boost factor to test minimum clamp
            MinModifier = 0.1f
        };

        var context = new WfcConstraintContext
        {
            Position = new Vector2I(5, 5),
            TileId = "fire_tile",
            Grid = _grid
        };

        // Act
        var modifier = constraint.GetProbabilityModifier(context);

        // Assert: Should never go below minimum
        AssertFloat(modifier).IsGreaterEqual(0.1f);
    }

    [TestCase]
    public void BiomeAffinityConstraint_ImplementsIWfcConstraint()
    {
        // Arrange
        var gradient = new FixedGradient(new CardSignature());
        var strengthGrid = new BiomeStrengthGrid(new Vector2I(10, 10), gradient, _registry);

        // Act
        var constraint = new BiomeAffinityConstraint(strengthGrid, _registry, _tileRegistry);

        // Assert
        AssertThat(constraint is IWfcConstraint).IsTrue();
    }

    /// <summary>
    /// Test gradient that returns a fixed signature for all positions.
    /// </summary>
    private sealed partial class FixedGradient : BaselineGradient
    {
        private readonly CardSignature _signature;

        public FixedGradient(CardSignature signature)
        {
            _signature = signature;
        }

        public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize)
        {
            return _signature;
        }
    }
}
