using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Modifiers;

[TestSuite]
[RequireGodotRuntime]
public class DiminishingReturnsSoftModifierTest
{
    private BlobSizeTracker _tracker = null!;
    private DiminishingReturnsSoftModifier _modifier = null!;
    private WfcGrid _grid = null!;

    [BeforeTest]
    public void Setup()
    {
        _tracker = new BlobSizeTracker();
        _modifier = new DiminishingReturnsSoftModifier(_tracker);
        _grid = new WfcGrid(10, 10, new[] { "grass", "water", "sand" });
    }

    [TestCase]
    public void TestSingleTileGetsMinimalDecay()
    {
        // With no neighbors, potential blob size = 1
        // Since 1 < MinimumBlobSize (default 30), no decay applied
        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Size 1 < MinimumBlobSize (30), so multiplier = 1.0 (no decay)
        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestBlobSize100GetsTargetedDecay()
    {
        // Create a large blob of 99 tiles (to simulate joining it making 100)
        for (var i = 0; i < 99; i++)
        {
            var x = i % 10;
            var y = i / 10;
            _tracker.RegisterCollapse(new Vector2I(x, y), "grass", _grid);
        }

        // Position (9, 9) would join this blob, making size 100
        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(9, 9)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // New formula: 1 / (1 + (100 - 30) * 0.05) = 1 / (1 + 70 * 0.05) = 1 / 4.5 ≈ 0.222
        AssertFloat(multiplier).IsBetween(0.20f, 0.25f);
    }

    [TestCase]
    public void TestDifferentTileTypeGetsNoDecayFromOtherBlobs()
    {
        // Create grass blob
        _tracker.RegisterCollapse(new Vector2I(4, 5), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(5, 5), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(6, 5), "grass", _grid);

        // Water tile next to grass blob
        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 4)),
            TileId = "water",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Water has no water neighbors, so potential size = 1
        // Size 1 < MinimumBlobSize (30), so multiplier = 1.0 (no decay)
        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestMinimumMultiplierEnforced()
    {
        _modifier.MinimumMultiplier = 0.1f;

        // Create very large blob
        for (var x = 0; x < 10; x++)
        {
            for (var y = 0; y < 9; y++)
            {
                _tracker.RegisterCollapse(new Vector2I(x, y), "grass", _grid);
            }
        }

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(0, 9)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        AssertFloat(multiplier).IsGreaterEqual(0.1f);
    }

    [TestCase]
    public void TestMinimumBlobSizeThreshold()
    {
        _modifier.MinimumBlobSize = 5;

        // Create small blob of 3 tiles
        _tracker.RegisterCollapse(new Vector2I(5, 5), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(5, 6), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(5, 7), "grass", _grid);

        // Adding tile would make blob size 4, still under threshold
        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 8)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Below minimum threshold, no decay applied
        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestCustomDecayFactor()
    {
        _modifier.DecayFactor = 0.5f;
        _modifier.MinimumBlobSize = 5; // Set low threshold so decay kicks in

        // Create blob of 9 tiles
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 3; y++)
            {
                _tracker.RegisterCollapse(new Vector2I(x, y), "grass", _grid);
            }
        }

        // Adding tile at (3, 0) would make blob size 10
        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(3, 0)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // New formula: 1 / (1 + (10 - 5) * 0.5) = 1 / (1 + 5 * 0.5) = 1 / 3.5 ≈ 0.286
        AssertFloat(multiplier).IsBetween(0.25f, 0.32f);
    }

    [TestCase]
    public void TestDecayFormulaIsCorrect()
    {
        // Verify the formula: 1 / (1 + (size - MinimumBlobSize) * DecayFactor)
        // With size=50, MinimumBlobSize=30, DecayFactor=0.05:
        // 1 / (1 + (50 - 30) * 0.05) = 1 / (1 + 20 * 0.05) = 1 / 2 = 0.5

        // Build 49-tile blob
        for (var i = 0; i < 49; i++)
        {
            var x = i % 7;
            var y = i / 7;
            _tracker.RegisterCollapse(new Vector2I(x, y), "grass", _grid);
        }

        // Position (0, 7) would join making size 50
        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(0, 7)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // 1 / (1 + (50 - 30) * 0.05) = 1 / 2 = 0.5
        AssertFloat(multiplier).IsBetween(0.45f, 0.55f);
    }

    [TestCase]
    public void TestContinuityBiasCancellation()
    {
        // Build 99-tile blob (adding one more makes 100)
        for (var i = 0; i < 99; i++)
        {
            var x = i % 10;
            var y = i / 10;
            _tracker.RegisterCollapse(new Vector2I(x, y), "grass", _grid);
        }

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(9, 9)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // New formula: At size 100: 1 / (1 + (100 - 30) * 0.05) = 1 / (1 + 70 * 0.05) = 1 / 4.5 ≈ 0.222
        // Combined with 5x continuity bias: 5.0 * 0.222 ≈ 1.11
        // At this size, continuity bias slightly outweighs decay (allowing large blobs with gentle penalty)
        var combinedEffect = 5.0f * multiplier;
        AssertFloat(combinedEffect).IsBetween(1.0f, 1.2f);
    }
}
