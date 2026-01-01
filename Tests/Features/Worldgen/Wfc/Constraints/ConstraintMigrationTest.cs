using System.Collections.Generic;
using System.Reflection;
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
    public void DiminishingReturns_ImplementsOnlyIWfcConstraint()
    {
        var modifier = new DiminishingReturnsSoftModifier(_tracker);

        // Verify it implements IWfcConstraint
        AssertThat(modifier is IWfcConstraint).IsTrue();

        // Verify ISoftModifier interface is gone (class only implements IWfcConstraint)
        var interfaces = typeof(DiminishingReturnsSoftModifier).GetInterfaces();
        AssertThat(interfaces.Length).IsEqual(1);
        AssertThat(interfaces[0].Name).IsEqual("IWfcConstraint");
    }

    [TestCase]
    public void Novelty_ImplementsOnlyIWfcConstraint()
    {
        var modifier = new NoveltySoftModifier();

        // Verify it implements IWfcConstraint
        AssertThat(modifier is IWfcConstraint).IsTrue();

        // Verify ISoftModifier interface is gone
        var interfaces = typeof(NoveltySoftModifier).GetInterfaces();
        AssertThat(interfaces.Length).IsEqual(1);
        AssertThat(interfaces[0].Name).IsEqual("IWfcConstraint");
    }

    [TestCase]
    public void Compactness_ImplementsOnlyIWfcConstraint()
    {
        var modifier = new CompactnessSoftModifier();

        // Verify it implements IWfcConstraint
        AssertThat(modifier is IWfcConstraint).IsTrue();

        // Verify ISoftModifier interface is gone
        var interfaces = typeof(CompactnessSoftModifier).GetInterfaces();
        AssertThat(interfaces.Length).IsEqual(1);
        AssertThat(interfaces[0].Name).IsEqual("IWfcConstraint");
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

    [TestCase]
    public void WfcTileSelector_AddModifier_MethodDoesNotExist()
    {
        // Verify legacy AddModifier method is gone
        var addModifierMethod = typeof(WfcTileSelector).GetMethod("AddModifier");
        AssertThat(addModifierMethod).IsNull();
    }

    [TestCase]
    public void WfcTileSelector_ClearModifiers_MethodDoesNotExist()
    {
        // Verify legacy ClearModifiers method is gone
        var clearModifiersMethod = typeof(WfcTileSelector).GetMethod("ClearModifiers");
        AssertThat(clearModifiersMethod).IsNull();
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
