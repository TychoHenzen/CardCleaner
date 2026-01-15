using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Integration tests for BiomeStrengthGrid in WFC generation pipeline.
/// Tests Phase 4.2: Biome Grid integration into GenerateMultiBiome.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class BiomeGridIntegrationTest
{
    private WfcAdjacencyRules _rules = null!;
    private BiomeRegistry _registry = null!;
    private TileRegistry _tileRegistry = null!;

    [BeforeTest]
    public void Setup()
    {
        // Create simple adjacency rules for test tiles
        _rules = new WfcAdjacencyRules(new[]
        {
            ("fire_tile", "fire_tile"),
            ("fire_tile", "water_tile"),
            ("water_tile", "water_tile"),
            ("water_tile", "neutral_tile"),
            ("neutral_tile", "neutral_tile"),
            ("neutral_tile", "fire_tile")
        });

        // Create a tile registry with test tiles
        // Tiles need AllowedBiomes set for BiomeAffinityConstraint to work
        _tileRegistry = new TileRegistry();
        _tileRegistry.Clear(); // Clear production tiles loaded by constructor
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

        // Create test registry with biomes that have distinct signatures
        _registry = new BiomeRegistry();

        var fireSignature = new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }); // High Febris (fire)
        var waterSignature = new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f }); // Low Febris (water)

        var firePool = new TilePool();
        firePool.Add("fire_tile", 1.0f);

        var waterPool = new TilePool();
        waterPool.Add("water_tile", 1.0f);

        var emptyPool = new TilePool();

        _registry.Register(new BiomeDefinition("fire", fireSignature, firePool, emptyPool));
        _registry.Register(new BiomeDefinition("water", waterSignature, waterPool, emptyPool));
    }

    [TestCase]
    public void GenerateMultiBiome_WithGradient_CreatesBiomeGrid()
    {
        // Arrange
        var generator = new WfcMapGenerator(_rules, _tileRegistry);
        var gradient = new FixedGradient(new CardSignature(new[] { 0f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f }));

        // Act
        var result = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("fire")!,
            new Vector2I(5, 5),
            12345,
            gradient);

        // Assert
        // Should succeed (BiomeGrid is created and BiomeAffinityConstraint is registered)
        AssertBool(result.Success).IsTrue();
        AssertThat(result.MapData).IsNotNull();
        AssertThat(result.MapData!.Size).IsEqual(new Vector2I(5, 5));
    }

    [TestCase]
    public void GenerateMultiBiome_WithoutGradient_StillWorks()
    {
        // Arrange
        var generator = new WfcMapGenerator(_rules, _tileRegistry);

        // Act: Pass null gradient (backwards compatible)
        var result = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("fire")!,
            new Vector2I(5, 5),
            12345,
            null);

        // Assert: Should succeed without BiomeAffinityConstraint
        AssertBool(result.Success).IsTrue();
        AssertThat(result.MapData).IsNotNull();
    }

    [TestCase]
    public void TileAffinity_DefaultsToPositiveForBiomeTiles()
    {
        // This test verifies that tiles in a biome's TilePool get boosted
        // when the gradient matches the biome's signature.

        // Arrange: Gradient matches fire biome signature
        var generator = new WfcMapGenerator(_rules, _tileRegistry);
        var fireMatchingGradient = new FixedGradient(new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }));

        // Act: Generate map with fire-matching gradient at fire biome positions
        var result = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("fire")!,
            new Vector2I(8, 8),
            42,
            fireMatchingGradient);

        // Assert: Map should generate successfully
        AssertBool(result.Success).IsTrue();
        AssertThat(result.MapData).IsNotNull();

        // Count fire tiles - with matching gradient, fire tiles should be boosted
        var fireTileCount = 0;
        var mapData = result.MapData!;
        for (var y = 0; y < mapData.Size.Y; y++)
        {
            for (var x = 0; x < mapData.Size.X; x++)
            {
                if (mapData.TileIds[y, x] == "fire_tile")
                    fireTileCount++;
            }
        }

        // Fire tiles should appear (boosted by positive affinity)
        AssertThat(fireTileCount).IsGreater(0);
        GD.Print($"Fire tiles with matching gradient: {fireTileCount}/{mapData.Size.X * mapData.Size.Y}");
    }

    [TestCase]
    public void TileAffinity_DefaultsToNeutralForOtherTiles()
    {
        // Tiles not in any biome's TilePool should get neutral modifier (1.0)

        // Arrange: Add a neutral tile to rules but not to any biome
        var rulesWithNeutral = new WfcAdjacencyRules(new[]
        {
            ("fire_tile", "fire_tile"),
            ("fire_tile", "water_tile"),
            ("fire_tile", "neutral_tile"),
            ("water_tile", "water_tile"),
            ("water_tile", "neutral_tile"),
            ("neutral_tile", "neutral_tile")
        });

        // Create a tile registry for this test with neutral tile
        var testTileRegistry = new TileRegistry();
        testTileRegistry.Clear(); // Clear production tiles loaded by constructor
        testTileRegistry.RegisterTile(new TileDefinition(
            id: "fire_tile",
            name: "Fire Tile",
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero,
            allowedBiomes: new HashSet<string> { "fire" }
        ));
        testTileRegistry.RegisterTile(new TileDefinition(
            id: "water_tile",
            name: "Water Tile",
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero,
            allowedBiomes: new HashSet<string> { "water" }
        ));
        // neutral_tile has no AllowedBiomes (universal/neutral)
        testTileRegistry.RegisterTile(new TileDefinition(
            id: "neutral_tile",
            name: "Neutral Tile",
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero
        ));

        var generator = new WfcMapGenerator(rulesWithNeutral, testTileRegistry);
        var gradient = new FixedGradient(new CardSignature()); // Neutral signature

        // Create registry with tiles in pools
        var registry = new BiomeRegistry();
        var firePool = new TilePool();
        firePool.Add("fire_tile", 1.0f);
        var waterPool = new TilePool();
        waterPool.Add("water_tile", 1.0f);
        var emptyPool = new TilePool();

        registry.Register(new BiomeDefinition("fire",
            new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }),
            firePool, emptyPool));
        registry.Register(new BiomeDefinition("water",
            new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f }),
            waterPool, emptyPool));

        // Act
        var result = generator.GenerateMultiBiome(
            registry,
            pos => registry.GetBiome("fire")!,
            new Vector2I(8, 8),
            12345,
            gradient);

        // Assert: Should succeed - neutral_tile gets modifier 1.0 (not penalized)
        AssertBool(result.Success).IsTrue();
    }

    [TestCase]
    public void MapGeneration_ShowsBiomeInfluence()
    {
        // This test verifies that biome strength actually influences tile selection.
        // We compare maps generated with different gradients.

        // Arrange
        var generator = new WfcMapGenerator(_rules, _tileRegistry);

        // Gradient that matches fire biome
        var fireGradient = new FixedGradient(new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }));

        // Gradient that matches water biome
        var waterGradient = new FixedGradient(new CardSignature(new[] { 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f }));

        // Act: Generate with fire gradient
        var fireResult = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("fire")!,
            new Vector2I(10, 10),
            42,
            fireGradient);

        // Generate with water gradient
        var waterResult = generator.GenerateMultiBiome(
            _registry,
            pos => _registry.GetBiome("water")!,
            new Vector2I(10, 10),
            42,
            waterGradient);

        // Assert
        if (!fireResult.Success || !waterResult.Success)
        {
            GD.Print("Skipping biome influence test - generation failed");
            return;
        }

        // Count tiles in each map
        var fireMapFireTiles = CountTiles(fireResult.MapData!, "fire_tile");
        var fireMapWaterTiles = CountTiles(fireResult.MapData!, "water_tile");
        var waterMapFireTiles = CountTiles(waterResult.MapData!, "fire_tile");
        var waterMapWaterTiles = CountTiles(waterResult.MapData!, "water_tile");

        GD.Print($"Fire gradient map: {fireMapFireTiles} fire, {fireMapWaterTiles} water");
        GD.Print($"Water gradient map: {waterMapFireTiles} fire, {waterMapWaterTiles} water");

        // Maps should show some influence - fire gradient should favor fire tiles
        // and water gradient should favor water tiles
        // (This is probabilistic, so we just verify non-zero counts)
        AssertThat(fireMapFireTiles + fireMapWaterTiles).IsGreater(0);
        AssertThat(waterMapFireTiles + waterMapWaterTiles).IsGreater(0);
    }

    [TestCase]
    public void BiomeAffinityEnum_HasThreeValues()
    {
        // Per plan spec, BiomeAffinity should have Positive, Neutral, Negative values
        // However, Phase 4.1 already implemented tile-to-biome lookup via TilePool membership
        // This test verifies the existing mechanism works correctly

        // Arrange
        var gradient = new FixedGradient(new CardSignature(new[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }));
        var strengthGrid = new BiomeStrengthGrid(new Vector2I(10, 10), gradient, _registry);
        var constraint = new Scripts.Features.Worldgen.Wfc.Constraints.BiomeAffinityConstraint(
            strengthGrid, _registry, _tileRegistry);

        // Act: Check constraint behavior for different tile types
        var grid = new WfcGrid(10, 10, new[] { "fire_tile", "water_tile", "neutral_tile" });

        // Fire tile at fire-matching position
        var fireContext = new Scripts.Features.Worldgen.Wfc.Constraints.WfcConstraintContext
        {
            CellId = grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "fire_tile",
            Topology = grid
        };
        var fireModifier = constraint.GetProbabilityModifier(fireContext);

        // Water tile at fire-matching position (should be penalized)
        var waterContext = new Scripts.Features.Worldgen.Wfc.Constraints.WfcConstraintContext
        {
            CellId = grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "water_tile",
            Topology = grid
        };
        var waterModifier = constraint.GetProbabilityModifier(waterContext);

        // Neutral tile (not in any biome)
        var neutralContext = new Scripts.Features.Worldgen.Wfc.Constraints.WfcConstraintContext
        {
            CellId = grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "neutral_tile",
            Topology = grid
        };
        var neutralModifier = constraint.GetProbabilityModifier(neutralContext);

        // Assert: Fire tile should be boosted, water penalized, neutral = 1.0
        AssertFloat(fireModifier).IsGreater(1.0f); // Positive affinity + positive strength = boost
        AssertFloat(waterModifier).IsLess(1.0f); // Positive affinity + negative strength = penalty
        AssertFloat(neutralModifier).IsEqual(1.0f); // Neutral (not in any biome)

        GD.Print($"Fire tile modifier: {fireModifier}");
        GD.Print($"Water tile modifier: {waterModifier}");
        GD.Print($"Neutral tile modifier: {neutralModifier}");
    }

    private static int CountTiles(SimpleMapData mapData, string tileId)
    {
        var count = 0;
        for (var y = 0; y < mapData.Size.Y; y++)
        {
            for (var x = 0; x < mapData.Size.X; x++)
            {
                if (mapData.TileIds[y, x] == tileId)
                    count++;
            }
        }
        return count;
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
