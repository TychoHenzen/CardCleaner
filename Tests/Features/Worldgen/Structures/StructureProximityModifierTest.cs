using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.WeightModifiers;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Structures;

[TestSuite]
[RequireGodotRuntime]
public class StructureProximityModifierTest
{
    private RandomNumberGenerator _rng = null!;
    private BiomeDefinition _testBiome = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
        _testBiome = new BiomeDefinition("plains", new CardSignature(), new TilePool(), new TilePool());
    }

    [TestCase]
    public void TestNoStructuresDoesNotModifyWeights()
    {
        var modifier = new StructureProximityModifier();
        var weights = new Dictionary<string, float>
        {
            ["grass"] = 1.0f,
            ["stone"] = 1.0f
        };
        var context = CreateContext(new Vector2I(5, 5), weights);

        modifier.ApplyModifier(context);

        AssertThat(weights["grass"]).IsEqual(1.0f);
        AssertThat(weights["stone"]).IsEqual(1.0f);
    }

    [TestCase]
    public void TestStructureOutsideRadiusDoesNotModify()
    {
        var modifier = new StructureProximityModifier();
        modifier.RegisterStructure(new PlacedStructureInfluence
        {
            Position = new Vector2I(0, 0),
            Size = new Vector2I(1, 1),
            InfluenceRadius = 3,
            TileAffinities = new Dictionary<string, float> { ["cobblestone"] = 2.0f }
        });

        var weights = new Dictionary<string, float> { ["cobblestone"] = 1.0f };
        var context = CreateContext(new Vector2I(10, 10), weights); // Far from structure

        modifier.ApplyModifier(context);

        AssertThat(weights["cobblestone"]).IsEqual(1.0f); // Unchanged
    }

    [TestCase]
    public void TestStructureInsideRadiusModifiesWeights()
    {
        var modifier = new StructureProximityModifier();
        modifier.RegisterStructure(new PlacedStructureInfluence
        {
            Position = new Vector2I(5, 5),
            Size = new Vector2I(1, 1),
            InfluenceRadius = 5,
            TileAffinities = new Dictionary<string, float> { ["cobblestone"] = 2.0f }
        });

        var weights = new Dictionary<string, float> { ["cobblestone"] = 1.0f };
        var context = CreateContext(new Vector2I(6, 5), weights); // 1 tile away

        modifier.ApplyModifier(context);

        // Should be boosted (multiplier > 1)
        AssertThat(weights["cobblestone"]).IsGreater(1.0f);
    }

    [TestCase]
    public void TestCloserToStructureMeansStrongerEffect()
    {
        var modifier = new StructureProximityModifier();
        modifier.RegisterStructure(new PlacedStructureInfluence
        {
            Position = new Vector2I(5, 5),
            Size = new Vector2I(1, 1),
            InfluenceRadius = 5,
            TileAffinities = new Dictionary<string, float> { ["cobblestone"] = 2.0f }
        });

        // Test at distance 1
        var weights1 = new Dictionary<string, float> { ["cobblestone"] = 1.0f };
        var context1 = CreateContext(new Vector2I(6, 5), weights1);
        modifier.ApplyModifier(context1);

        // Test at distance 3
        var weights3 = new Dictionary<string, float> { ["cobblestone"] = 1.0f };
        var context3 = CreateContext(new Vector2I(8, 5), weights3);
        modifier.ApplyModifier(context3);

        // Closer should have stronger boost
        AssertThat(weights1["cobblestone"]).IsGreater(weights3["cobblestone"]);
    }

    [TestCase]
    public void TestNegativeAffinityReducesWeight()
    {
        var modifier = new StructureProximityModifier();
        modifier.RegisterStructure(new PlacedStructureInfluence
        {
            Position = new Vector2I(5, 5),
            Size = new Vector2I(1, 1),
            InfluenceRadius = 5,
            TileAffinities = new Dictionary<string, float> { ["grass"] = 0.3f } // Reduce grass near structure
        });

        var weights = new Dictionary<string, float> { ["grass"] = 1.0f };
        var context = CreateContext(new Vector2I(6, 5), weights);

        modifier.ApplyModifier(context);

        AssertThat(weights["grass"]).IsLess(1.0f);
    }

    [TestCase]
    public void TestMinWeightPreventsZero()
    {
        var modifier = new StructureProximityModifier { MinWeight = 0.1f };
        modifier.RegisterStructure(new PlacedStructureInfluence
        {
            Position = new Vector2I(5, 5),
            Size = new Vector2I(1, 1),
            InfluenceRadius = 5,
            TileAffinities = new Dictionary<string, float> { ["grass"] = 0.01f } // Very strong reduction
        });

        var weights = new Dictionary<string, float> { ["grass"] = 0.05f }; // Already low
        var context = CreateContext(new Vector2I(5, 5), weights); // Right at structure

        modifier.ApplyModifier(context);

        AssertThat(weights["grass"]).IsGreaterEqual(0.1f); // Should not go below MinWeight
    }

    [TestCase]
    public void TestMultiTileStructureDistanceCalculation()
    {
        var modifier = new StructureProximityModifier();
        modifier.RegisterStructure(new PlacedStructureInfluence
        {
            Position = new Vector2I(0, 0),
            Size = new Vector2I(3, 3), // 3x3 structure at origin
            InfluenceRadius = 3,
            TileAffinities = new Dictionary<string, float> { ["special"] = 2.0f }
        });

        // Position adjacent to structure edge should be affected
        var weightsAdjacent = new Dictionary<string, float> { ["special"] = 1.0f };
        var contextAdjacent = CreateContext(new Vector2I(3, 1), weightsAdjacent); // Just outside structure
        modifier.ApplyModifier(contextAdjacent);

        AssertThat(weightsAdjacent["special"]).IsGreater(1.0f);
    }

    [TestCase]
    public void TestClearRemovesAllStructures()
    {
        var modifier = new StructureProximityModifier();
        modifier.RegisterStructure(new PlacedStructureInfluence
        {
            Position = new Vector2I(5, 5),
            Size = new Vector2I(1, 1),
            InfluenceRadius = 5,
            TileAffinities = new Dictionary<string, float> { ["cobblestone"] = 2.0f }
        });

        modifier.Clear();

        AssertThat(modifier.PlacedStructures.Count).IsEqual(0);
    }

    private TileSelectionContext CreateContext(Vector2I position, Dictionary<string, float> weights)
    {
        return new TileSelectionContext(
            position,
            new Dictionary<Vector2I, string>(),
            _testBiome,
            _rng,
            weights);
    }
}
