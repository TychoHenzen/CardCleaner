using System.IO;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TransitionMap;

/// <summary>
///     TransitionMapStructureTest scenarios split out of TransitionMapValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TransitionMapStructureTest : TransitionMapValidationTestBase
{
    // ==================== File Existence ====================

    [TestCase]
    public void TestTransitionMapFileExists()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        AssertBool(File.Exists(absolutePath)).IsTrue();
    }

    [TestCase]
    public void TestTransitionMapHasRequiredFields()
    {
        AssertThat(_transitionDoc).IsNotNull();
        var root = _transitionDoc!.RootElement;

        AssertBool(root.TryGetProperty("version", out _)).IsTrue();
        AssertBool(root.TryGetProperty("transitions", out _)).IsTrue();
    }

    // ==================== Variant Count Validation ====================

    [TestCase]
    public void TestCorner16TransitionsHave16Variants()
    {
        AssertThat(_transitionMap).IsNotNull();

        var incorrectCounts = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length != 16)
                incorrectCounts.Add($"{key}: corner16 has {entry.Variants.Length} variants (expected 16)");
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect corner16 variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47TransitionsHave47Variants()
    {
        AssertThat(_transitionMap).IsNotNull();

        var incorrectCounts = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "blob47")
                continue;

            if (entry.Variants.Length != 47)
                incorrectCounts.Add($"{key}: blob47 has {entry.Variants.Length} variants (expected 47)");
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect blob47 variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }
}
