using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen;

[TestSuite]
[RequireGodotRuntime]
public class GradientInfluenceComponentTest
{
    private static readonly CompatibilityTag Any = new() { Tag = "Any", Mode = CompatibilityTag.CompatibilityMode.Any };

    private static SemanticTile CreateTestTileWithSignature(string name, CardSignature? signature, float baseWeight = 1.0f)
    {
        return new SemanticTile
        {
            TileName = name,
            Signature = signature,
            BaseWeight = baseWeight,
            SocketData = new SocketData
            {
                North = new Godot.Collections.Array<CompatibilityTag> { Any },
                East = new Godot.Collections.Array<CompatibilityTag> { Any },
                South = new Godot.Collections.Array<CompatibilityTag> { Any },
                West = new Godot.Collections.Array<CompatibilityTag> { Any },
                NorthEast = new Godot.Collections.Array<CompatibilityTag> { Any },
                NorthWest = new Godot.Collections.Array<CompatibilityTag> { Any },
                SouthEast = new Godot.Collections.Array<CompatibilityTag> { Any },
                SouthWest = new Godot.Collections.Array<CompatibilityTag> { Any },
                Up = new Godot.Collections.Array<CompatibilityTag> { Any },
                Down = new Godot.Collections.Array<CompatibilityTag> { Any }
            },
            Passability = TilePassability.Passable,
            Layer = TileLayer.Terrain
        };
    }

    [TestCase]
    public void TestSamePositionDifferentTilesGetDifferentWeights()
    {
        // Arrange: Create gradient with distinct center signature
        var centerSignature = new CardSignature(new float[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        var edgeSignature = new CardSignature(new float[] { -1, 0, 0, 0, 0, 0, 0, 0 });
        var gradient = new RadialGradient
        {
            CenterSignature = centerSignature,
            EdgeSignature = edgeSignature
        };
        var component = new GradientInfluenceComponent(gradient, 1.0f);

        // Create tiles with opposite signatures
        var matchingTile = CreateTestTileWithSignature("Matching", new CardSignature(new float[] { 1, 0, 0, 0, 0, 0, 0, 0 }));
        var oppositeTile = CreateTestTileWithSignature("Opposite", new CardSignature(new float[] { -1, 0, 0, 0, 0, 0, 0, 0 }));
        var tiles = new List<SemanticTile> { matchingTile, oppositeTile };

        // Act: Get weights at center position (where gradient matches centerSignature)
        var centerPosition = new Vector3I(5, 5, 0);
        var mapSize = new Vector3I(10, 10, 1);
        var weights = component.AdjustTileWeights(tiles, centerPosition, mapSize);

        // Assert: Matching tile should have higher weight
        AssertFloat(weights[0]).IsGreater(weights[1]);
    }

    [TestCase]
    public void TestDifferentPositionsSameTileGetDifferentWeights()
    {
        // Arrange: Create radial gradient
        var centerSignature = new CardSignature(new float[] { 1, 1, 1, 1, 1, 1, 1, 1 });
        var edgeSignature = new CardSignature(new float[] { -1, -1, -1, -1, -1, -1, -1, -1 });
        var gradient = new RadialGradient
        {
            CenterSignature = centerSignature,
            EdgeSignature = edgeSignature,
            Falloff = 1.0f
        };
        var component = new GradientInfluenceComponent(gradient, 1.0f);

        // Create tile that matches center signature
        var tile = CreateTestTileWithSignature("CenterAligned", new CardSignature(new float[] { 1, 1, 1, 1, 1, 1, 1, 1 }));
        var tiles = new List<SemanticTile> { tile };
        var mapSize = new Vector3I(20, 20, 1);

        // Act: Get weights at center vs edge positions
        var centerPosition = new Vector3I(10, 10, 0);
        var edgePosition = new Vector3I(0, 0, 0);
        var centerWeights = component.AdjustTileWeights(tiles, centerPosition, mapSize);
        var edgeWeights = component.AdjustTileWeights(tiles, edgePosition, mapSize);

        // Assert: Same tile should have higher weight at center (where gradient matches tile signature)
        AssertFloat(centerWeights[0]).IsGreater(edgeWeights[0]);
    }

    [TestCase]
    public void TestTilesWithSimilarSignatureToGradientHaveHigherWeights()
    {
        // Arrange: Create gradient with specific signature at position
        var centerSignature = new CardSignature(new float[] { 0.8f, 0.6f, 0.4f, 0.2f, 0, 0, 0, 0 });
        var edgeSignature = new CardSignature(new float[] { -0.8f, -0.6f, -0.4f, -0.2f, 0, 0, 0, 0 });
        var gradient = new RadialGradient
        {
            CenterSignature = centerSignature,
            EdgeSignature = edgeSignature
        };
        var component = new GradientInfluenceComponent(gradient, 1.0f);

        // Create tiles with varying similarity to center signature
        var similarTile = CreateTestTileWithSignature("Similar", new CardSignature(new float[] { 0.7f, 0.5f, 0.3f, 0.1f, 0, 0, 0, 0 }));
        var dissimilarTile = CreateTestTileWithSignature("Dissimilar", new CardSignature(new float[] { -0.7f, -0.5f, -0.3f, -0.1f, 0, 0, 0, 0 }));
        var neutralTile = CreateTestTileWithSignature("Neutral", new CardSignature(new float[] { 0, 0, 0, 0, 0, 0, 0, 0 }));
        var tiles = new List<SemanticTile> { similarTile, dissimilarTile, neutralTile };

        // Act: Get weights at center position
        var centerPosition = new Vector3I(5, 5, 0);
        var mapSize = new Vector3I(10, 10, 1);
        var weights = component.AdjustTileWeights(tiles, centerPosition, mapSize);

        // Assert: Similar tile > Neutral tile > Dissimilar tile
        AssertFloat(weights[0]).IsGreater(weights[2]); // Similar > Neutral
        AssertFloat(weights[2]).IsGreater(weights[1]); // Neutral > Dissimilar
    }

    [TestCase]
    public void TestTilesWithoutSignatureFallBackToBaseWeight()
    {
        // Arrange
        var centerSignature = new CardSignature(new float[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        var edgeSignature = new CardSignature(new float[] { -1, 0, 0, 0, 0, 0, 0, 0 });
        var gradient = new RadialGradient
        {
            CenterSignature = centerSignature,
            EdgeSignature = edgeSignature
        };
        var component = new GradientInfluenceComponent(gradient, 1.0f);

        // Create tile without signature (null)
        var tileWithoutSignature = CreateTestTileWithSignature("NoSignature", null, 5.0f);
        var tiles = new List<SemanticTile> { tileWithoutSignature };

        // Act: Get weights at any position
        var position = new Vector3I(5, 5, 0);
        var mapSize = new Vector3I(10, 10, 1);
        var weights = component.AdjustTileWeights(tiles, position, mapSize);

        // Assert: Weight should equal base weight (no adjustment without signature)
        AssertFloat(weights[0]).IsEqual(5.0f);
    }

    [TestCase]
    public void TestInfluenceStrengthAffectsWeightAdjustment()
    {
        // Arrange: Create same gradient with different influence strengths
        var centerSignature = new CardSignature(new float[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        var edgeSignature = new CardSignature(new float[] { -1, 0, 0, 0, 0, 0, 0, 0 });
        var gradient = new RadialGradient
        {
            CenterSignature = centerSignature,
            EdgeSignature = edgeSignature
        };
        var lowInfluenceComponent = new GradientInfluenceComponent(gradient, 0.1f);
        var highInfluenceComponent = new GradientInfluenceComponent(gradient, 2.0f);

        // Create tile matching center signature
        var matchingTile = CreateTestTileWithSignature("Matching", new CardSignature(new float[] { 1, 0, 0, 0, 0, 0, 0, 0 }));
        var tiles = new List<SemanticTile> { matchingTile };

        // Act
        var centerPosition = new Vector3I(5, 5, 0);
        var mapSize = new Vector3I(10, 10, 1);
        var lowInfluenceWeights = lowInfluenceComponent.AdjustTileWeights(tiles, centerPosition, mapSize);
        var highInfluenceWeights = highInfluenceComponent.AdjustTileWeights(tiles, centerPosition, mapSize);

        // Assert: Higher influence should produce greater weight adjustment from base
        var lowDelta = Mathf.Abs(lowInfluenceWeights[0] - matchingTile.BaseWeight);
        var highDelta = Mathf.Abs(highInfluenceWeights[0] - matchingTile.BaseWeight);
        AssertFloat(highDelta).IsGreater(lowDelta);
    }

    [TestCase]
    public void TestNullGradientFallsBackToBaseWeights()
    {
        // Arrange: Create component with null gradient (edge case - shouldn't happen in practice
        // but AdjustTileWeights has null check)
        var component = new GradientInfluenceComponent(null!, 1.0f);

        var tile1 = CreateTestTileWithSignature("Tile1", new CardSignature(new float[] { 1, 0, 0, 0, 0, 0, 0, 0 }), 2.0f);
        var tile2 = CreateTestTileWithSignature("Tile2", new CardSignature(new float[] { -1, 0, 0, 0, 0, 0, 0, 0 }), 3.0f);
        var tiles = new List<SemanticTile> { tile1, tile2 };

        // Act
        var position = new Vector3I(5, 5, 0);
        var mapSize = new Vector3I(10, 10, 1);
        var weights = component.AdjustTileWeights(tiles, position, mapSize);

        // Assert: Should return base weights
        AssertFloat(weights[0]).IsEqual(2.0f);
        AssertFloat(weights[1]).IsEqual(3.0f);
    }

    [TestCase]
    public void TestWeightOrderPreservedInList()
    {
        // Arrange
        var centerSignature = new CardSignature(new float[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        var edgeSignature = new CardSignature(new float[] { -1, 0, 0, 0, 0, 0, 0, 0 });
        var gradient = new RadialGradient
        {
            CenterSignature = centerSignature,
            EdgeSignature = edgeSignature
        };
        var component = new GradientInfluenceComponent(gradient, 0.5f);

        var tile1 = CreateTestTileWithSignature("Tile1", new CardSignature(new float[] { 0.5f, 0, 0, 0, 0, 0, 0, 0 }));
        var tile2 = CreateTestTileWithSignature("Tile2", new CardSignature(new float[] { -0.5f, 0, 0, 0, 0, 0, 0, 0 }));
        var tile3 = CreateTestTileWithSignature("Tile3", new CardSignature(new float[] { 0, 0, 0, 0, 0, 0, 0, 0 }));
        var tiles = new List<SemanticTile> { tile1, tile2, tile3 };

        // Act
        var position = new Vector3I(5, 5, 0);
        var mapSize = new Vector3I(10, 10, 1);
        var weights = component.AdjustTileWeights(tiles, position, mapSize);

        // Assert: Weights list should have same count as tiles list
        AssertInt(weights.Count).IsEqual(tiles.Count);
    }

    [TestCase]
    public void TestEmptyTileListReturnsEmptyWeights()
    {
        // Arrange
        var gradient = new RadialGradient
        {
            CenterSignature = new CardSignature(),
            EdgeSignature = new CardSignature()
        };
        var component = new GradientInfluenceComponent(gradient, 1.0f);
        var tiles = new List<SemanticTile>();

        // Act
        var position = new Vector3I(5, 5, 0);
        var mapSize = new Vector3I(10, 10, 1);
        var weights = component.AdjustTileWeights(tiles, position, mapSize);

        // Assert
        AssertInt(weights.Count).IsEqual(0);
    }

    [TestCase]
    public void TestGeneratorUsesGradientInfluenceForTileSelection()
    {
        // Arrange: Create strongly biased gradient
        var fireSignature = new CardSignature(new float[] { 0, 1, 0, 0, 0, 0, 0, 0 }); // Febris = fire
        var waterSignature = new CardSignature(new float[] { 0, -1, 0, 0, 0, 0, 0, 0 }); // Febris = water
        var gradient = new RadialGradient
        {
            CenterSignature = fireSignature,
            EdgeSignature = fireSignature, // All fire - uniform gradient
            Falloff = 1.0f
        };
        var component = new GradientInfluenceComponent(gradient, 2.0f);

        // Create tiles - fire-aligned should be heavily favored
        var fireTile = CreateTestTileWithSignature("Fire", new CardSignature(new float[] { 0, 1, 0, 0, 0, 0, 0, 0 }));
        var waterTile = CreateTestTileWithSignature("Water", new CardSignature(new float[] { 0, -1, 0, 0, 0, 0, 0, 0 }));
        var tileSet = new[] { fireTile, waterTile };

        var mapSize = new Vector3I(10, 10, 1);
        var rng = new RandomNumberGenerator { Seed = 42 };

        // Act: Generate map
        var generator = new SemanticWfc3dGenerator(tileSet, mapSize, rng, component);
        var result = generator.Generate();

        // Count tiles
        var fireCount = 0;
        var waterCount = 0;
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
        {
            var tile = result[0, y, x];
            if (tile?.TileName == "Fire") fireCount++;
            else if (tile?.TileName == "Water") waterCount++;
        }

        // Assert: Fire tiles should significantly outnumber water tiles due to gradient influence
        AssertInt(fireCount).IsGreater(waterCount);
    }
}
