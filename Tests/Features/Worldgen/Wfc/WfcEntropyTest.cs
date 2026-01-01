using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

[TestSuite]
public class WfcEntropyTest
{
    [TestCase]
    public void GetWeightedEntropy_SingleTile_ReturnsZero()
    {
        // Collapsed cell has zero entropy
        var cell = new WfcCellState(new[] { "grass" });
        var weights = new Dictionary<string, float> { ["grass"] = 1.0f };

        var entropy = cell.GetWeightedEntropy(weights);

        AssertFloat(entropy).IsEqual(0f);
    }

    [TestCase]
    public void GetWeightedEntropy_UniformWeights_ReturnsMaxEntropy()
    {
        // Equal weights = highest entropy (most uncertainty)
        var cell = new WfcCellState(new[] { "grass", "dirt", "sand", "stone" });
        var weights = new Dictionary<string, float>
        {
            ["grass"] = 1.0f,
            ["dirt"] = 1.0f,
            ["sand"] = 1.0f,
            ["stone"] = 1.0f
        };

        var entropy = cell.GetWeightedEntropy(weights);

        // Shannon entropy for uniform distribution of 4 items = ln(4) ≈ 1.386
        AssertFloat(entropy).IsBetween(1.38f, 1.40f);
    }

    [TestCase]
    public void GetWeightedEntropy_DominantTile_ReturnsLowEntropy()
    {
        // 90% weight on one tile = low entropy (high certainty)
        var cell = new WfcCellState(new[] { "grass", "dirt" });
        var weights = new Dictionary<string, float>
        {
            ["grass"] = 9.0f, // 90%
            ["dirt"] = 1.0f   // 10%
        };

        var entropyDominant = cell.GetWeightedEntropy(weights);

        // Compare to uniform distribution (50/50)
        var uniformWeights = new Dictionary<string, float>
        {
            ["grass"] = 1.0f,
            ["dirt"] = 1.0f
        };
        var entropyUniform = cell.GetWeightedEntropy(uniformWeights);

        // Dominant tile should have lower entropy
        AssertFloat(entropyDominant).IsLess(entropyUniform);

        // Dominant entropy should be significantly lower
        // For 90/10 split: entropy ≈ 0.325
        AssertFloat(entropyDominant).IsLess(0.4f);
    }

    [TestCase]
    public void GetWeightedEntropy_Deterministic()
    {
        // Same weights always return same entropy
        var cell = new WfcCellState(new[] { "grass", "dirt", "sand" });
        var weights = new Dictionary<string, float>
        {
            ["grass"] = 2.0f,
            ["dirt"] = 1.0f,
            ["sand"] = 0.5f
        };

        var entropy1 = cell.GetWeightedEntropy(weights);
        var entropy2 = cell.GetWeightedEntropy(weights);
        var entropy3 = cell.GetWeightedEntropy(weights);

        AssertFloat(entropy1).IsEqual(entropy2);
        AssertFloat(entropy2).IsEqual(entropy3);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetLowestEntropyCellWeighted_SelectsDominantCell()
    {
        // Cell with clearest winner (dominant weight) should be selected
        var grid = new WfcGrid(2, 2, new[] { "grass", "dirt", "sand" });

        // Give position (0,0) a dominant tile (low entropy)
        // Give position (1,1) uniform weights (high entropy)
        // Collapse (0,1) and (1,0) so they're not candidates
        grid.GetCell(0, 1).CollapseTo("grass");
        grid.GetCell(1, 0).CollapseTo("dirt");

        var rng = new RandomNumberGenerator { Seed = 42 };

        IReadOnlyDictionary<string, float> GetWeightsAt(Vector2I pos)
        {
            if (pos.X == 0 && pos.Y == 0)
            {
                // Dominant tile - low entropy
                return new Dictionary<string, float>
                {
                    ["grass"] = 10.0f,
                    ["dirt"] = 0.1f,
                    ["sand"] = 0.1f
                };
            }
            else
            {
                // Uniform - high entropy
                return new Dictionary<string, float>
                {
                    ["grass"] = 1.0f,
                    ["dirt"] = 1.0f,
                    ["sand"] = 1.0f
                };
            }
        }

        var selected = grid.GetLowestEntropyCellWeighted(GetWeightsAt, rng);

        AssertThat(selected).IsNotNull();
        AssertThat(selected!.Value).IsEqual(new Vector2I(0, 0));
    }

    [TestCase]
    public void GetWeightedEntropy_MissingWeight_DefaultsToOne()
    {
        // Missing weights should default to 1.0
        var cell = new WfcCellState(new[] { "grass", "dirt" });
        var weights = new Dictionary<string, float>
        {
            ["grass"] = 1.0f
            // dirt is missing - should default to 1.0
        };

        var entropy = cell.GetWeightedEntropy(weights);

        // Should be same as uniform distribution
        var uniformWeights = new Dictionary<string, float>
        {
            ["grass"] = 1.0f,
            ["dirt"] = 1.0f
        };
        var uniformEntropy = cell.GetWeightedEntropy(uniformWeights);

        AssertFloat(entropy).IsEqual(uniformEntropy);
    }

    [TestCase]
    public void GetWeightedEntropy_ZeroWeight_ExcludedFromCalculation()
    {
        // Zero weight tiles are excluded from probability calculation
        var cell = new WfcCellState(new[] { "grass", "dirt", "sand" });
        var weights = new Dictionary<string, float>
        {
            ["grass"] = 1.0f,
            ["dirt"] = 1.0f,
            ["sand"] = 0.0f // Should be excluded
        };

        var entropy = cell.GetWeightedEntropy(weights);

        // Should be same as 2-tile uniform (grass + dirt only)
        var twoTileCell = new WfcCellState(new[] { "grass", "dirt" });
        var twoTileWeights = new Dictionary<string, float>
        {
            ["grass"] = 1.0f,
            ["dirt"] = 1.0f
        };
        var twoTileEntropy = twoTileCell.GetWeightedEntropy(twoTileWeights);

        AssertFloat(entropy).IsEqual(twoTileEntropy);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetLowestEntropyCellWeighted_AllCollapsed_ReturnsNull()
    {
        var grid = new WfcGrid(2, 2, new[] { "grass", "dirt" });

        // Collapse all cells
        grid.GetCell(0, 0).CollapseTo("grass");
        grid.GetCell(0, 1).CollapseTo("dirt");
        grid.GetCell(1, 0).CollapseTo("grass");
        grid.GetCell(1, 1).CollapseTo("dirt");

        var rng = new RandomNumberGenerator { Seed = 42 };
        IReadOnlyDictionary<string, float> GetWeightsAt(Vector2I pos) =>
            new Dictionary<string, float> { ["grass"] = 1.0f, ["dirt"] = 1.0f };

        var selected = grid.GetLowestEntropyCellWeighted(GetWeightsAt, rng);

        AssertThat(selected).IsNull();
    }
}
