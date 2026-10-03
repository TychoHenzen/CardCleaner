using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.Blob47Pattern;

/// <summary>
///     Blob47TransitionMapVariantTest scenarios split out of Blob47PatternValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Blob47TransitionMapVariantTest
{
    private const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";

    // ==================== Transition Map Blob47 Validation ====================

    [TestCase]
    public void TestBlob47TransitionsInMapHaveCorrectVariantCount()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (!File.Exists(absolutePath))
        {
            GD.PrintErr("transition_map.json not found");
            return;
        }

        var json = File.ReadAllText(absolutePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("transitions", out var transitions))
        {
            GD.PrintErr("No transitions in map");
            return;
        }

        var incorrectCounts = new List<string>();

        foreach (var transition in transitions.EnumerateObject())
        {
            var entry = transition.Value;
            if (!entry.TryGetProperty("format", out var formatProp))
                continue;

            var format = formatProp.GetString();
            if (format != "blob47")
                continue;

            if (!entry.TryGetProperty("variants", out var variants))
            {
                incorrectCounts.Add($"{transition.Name}: no variants array");
                continue;
            }

            var count = variants.GetArrayLength();
            if (count != 47)
                incorrectCounts.Add($"{transition.Name}: blob47 has {count} variants (expected 47)");
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect blob47 variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47TransitionsHaveVariantAtIndex0And46()
    {
        // Index 0 = isolated (0), Index 46 = solid fill (255)
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (!File.Exists(absolutePath))
            return;

        var json = File.ReadAllText(absolutePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("transitions", out var transitions))
            return;

        var missingVariants = new List<string>();

        foreach (var transition in transitions.EnumerateObject())
        {
            var entry = transition.Value;
            if (!entry.TryGetProperty("format", out var formatProp))
                continue;

            if (formatProp.GetString() != "blob47")
                continue;

            if (!entry.TryGetProperty("variants", out var variants))
                continue;

            var variantArray = variants.EnumerateArray().ToArray();
            if (variantArray.Length < 47)
                continue;

            if (variantArray[0].ValueKind == JsonValueKind.Null)
                missingVariants.Add($"{transition.Name}: variant[0] (isolated) is null");

            if (variantArray[46].ValueKind == JsonValueKind.Null)
                missingVariants.Add($"{transition.Name}: variant[46] (solid fill) is null");
        }

        if (missingVariants.Count > 0)
            GD.PrintErr($"Missing critical blob47 variants:\n{string.Join("\n", missingVariants)}");

        AssertThat(missingVariants.Count).IsEqual(0);
    }
}
