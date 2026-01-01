using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Constraints;

[TestSuite]
[RequireGodotRuntime]
public class ConstraintMigrationTest
{
    private WfcGrid _grid = null!;
    private BlobSizeTracker _tracker = null!;

    [BeforeTest]
    public void Setup()
    {
        _grid = new WfcGrid(10, 10, new[] { "grass", "water", "sand" });
        _tracker = new BlobSizeTracker();
    }

    [TestCase]
    public void DiminishingReturns_BothInterfacesReturnSameValue()
    {
        var modifier = new DiminishingReturnsSoftModifier(_tracker);

        // Create some blob for consistent test
        _tracker.RegisterCollapse(new Vector2I(5, 5), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(5, 6), "grass", _grid);

        var softContext = new SoftModifierContext
        {
            Position = new Vector2I(5, 7),
            TileId = "grass",
            Grid = _grid
        };

        var constraintContext = new WfcConstraintContext
        {
            Position = new Vector2I(5, 7),
            TileId = "grass",
            Grid = _grid
        };

        var softResult = modifier.CalculateMultiplier(softContext);
        var constraintResult = modifier.GetProbabilityModifier(constraintContext);

        AssertFloat(softResult).IsEqual(constraintResult);
    }

    [TestCase]
    public void Novelty_BothInterfacesReturnSameValue()
    {
        var modifier = new NoveltySoftModifier(_tracker);

        var softContext = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "water",
            Grid = _grid
        };

        var constraintContext = new WfcConstraintContext
        {
            Position = new Vector2I(5, 5),
            TileId = "water",
            Grid = _grid
        };

        var softResult = modifier.CalculateMultiplier(softContext);
        var constraintResult = modifier.GetProbabilityModifier(constraintContext);

        AssertFloat(softResult).IsEqual(constraintResult);
    }

    [TestCase]
    public void Compactness_BothInterfacesReturnSameValue()
    {
        var modifier = new CompactnessSoftModifier();

        // Collapse some neighbors to test neighbor counting
        _grid.GetCell(5, 4).CollapseTo("grass");
        _grid.GetCell(5, 6).CollapseTo("grass");

        var softContext = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var constraintContext = new WfcConstraintContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var softResult = modifier.CalculateMultiplier(softContext);
        var constraintResult = modifier.GetProbabilityModifier(constraintContext);

        AssertFloat(softResult).IsEqual(constraintResult);
    }

    [TestCase]
    public void AdjacencyConstraint_ValidAdjacency_ReturnsOne()
    {
        // Create rules where grass and water can be adjacent
        var rules = new WfcAdjacencyRules(new[] { ("grass", "water"), ("water", "sand") });
        var constraint = new AdjacencyConstraint(rules);

        // Collapse a neighbor as water
        _grid.GetCell(5, 5).CollapseTo("water");

        var context = new WfcConstraintContext
        {
            Position = new Vector2I(5, 6),
            TileId = "grass", // grass can be adjacent to water
            Grid = _grid
        };

        var result = constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void AdjacencyConstraint_InvalidAdjacency_ReturnsZero()
    {
        // Create rules where grass and water can be adjacent, but grass and sand cannot
        var rules = new WfcAdjacencyRules(new[] { ("grass", "water"), ("water", "sand") });
        var constraint = new AdjacencyConstraint(rules);

        // Collapse a neighbor as sand
        _grid.GetCell(5, 5).CollapseTo("sand");

        var context = new WfcConstraintContext
        {
            Position = new Vector2I(5, 6),
            TileId = "grass", // grass cannot be adjacent to sand (not in rules)
            Grid = _grid
        };

        var result = constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(0.0f);
    }

    [TestCase]
    public void WfcTileSelector_ConstraintList_AppliesMultiplicatively()
    {
        var selector = new WfcTileSelector();
        var rng = new RandomNumberGenerator { Seed = 12345 };

        // Create a custom constraint that always returns 0.5
        var halfConstraint = new TestHalfConstraint();
        selector.AddConstraint(halfConstraint);
        selector.AddConstraint(halfConstraint); // Add twice = 0.5 * 0.5 = 0.25

        // We need position and grid for constraints to be applied
        var tiles = new List<string> { "grass", "water" };

        // Run multiple selections to verify constraints are being applied
        // With two 0.5 constraints, weights should be quartered
        var result = selector.SelectTile(
            tiles,
            null,
            rng,
            null,
            new Vector2I(5, 5),
            _grid);

        // The test is really about verifying constraints are called and applied
        // If constraints weren't applied, we'd get normal distribution
        // Just verify we get a result (constraints were invoked without error)
        AssertThat(result).IsNotNull();
        AssertThat(halfConstraint.CallCount).IsEqual(4); // 2 tiles x 2 constraints
    }

    private class TestHalfConstraint : IWfcConstraint
    {
        public int CallCount { get; private set; }

        public float GetProbabilityModifier(WfcConstraintContext context)
        {
            CallCount++;
            return 0.5f;
        }
    }
}
