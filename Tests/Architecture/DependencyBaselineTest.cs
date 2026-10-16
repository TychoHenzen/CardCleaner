using System;
using System.IO;
using System.Linq;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture baselines for the ratchet of the dependency gate (#240 AC-5, #299).</summary>
[TestSuite]
public class DependencyBaselineTest
{
    private const string LoweringInstruction = "lower the baseline with";

    private static readonly GatedNumbers Baseline = new(10, 4, 5, 50);

    private const string BaselineJson = "{\"up_edges\": 10, \"biggest_loop\": 4, \"seams_leaky_or_missing\": 5, " +
                                        "\"refs_from_non_ui_to_ui\": 50}";

    private string _path = null!;

    [BeforeTest]
    public void CreatePath() =>
        _path = Path.Combine(Path.GetTempPath(), $"dependency-baseline-{Guid.NewGuid():N}.json");

    [AfterTest]
    public void DeleteFile()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PriorityOrderIsPinned()
    {
        AssertThat(string.Join(",", GatedNumbers.Fields.Select(f => f.Name)))
            .IsEqual("up_edges,biggest_loop,seams_leaky_or_missing,refs_from_non_ui_to_ui");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EqualMeasurementPasses()
    {
        Record(BaselineJson);

        var result = DependencyBaseline.Apply(_path, Baseline, update: false);

        AssertThat(result.Failures.Count).IsEqual(0);
        AssertThat(result.AcceptedRises.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HigherNumberFailsNamingTheNumber()
    {
        Record(BaselineJson);

        var result = DependencyBaseline.Apply(_path, Baseline with { UpEdges = 11 }, update: false);

        AssertThat(result.Failures.Count).IsEqual(1);
        AssertThat(result.Failures[0]).IsEqual("up_edges is 11, above the baseline 10");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LowerNumberFailsWithTheLoweringCommand()
    {
        Record(BaselineJson);

        var result = DependencyBaseline.Apply(_path, Baseline with { SeamsLeakyOrMissing = 4 }, update: false);

        AssertThat(result.Failures.Count).IsEqual(1);
        AssertThat(result.Failures[0])
            .StartsWith("seams_leaky_or_missing is 4, below the baseline 5: lower the baseline with " +
                        DependencyBaseline.SwitchVariable + "=1");
        AssertThat(result.AcceptedRises.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LowerDecidingNumberAcceptsAndNamesALowerPriorityRise()
    {
        Record(BaselineJson);

        var changed = Baseline with { SeamsLeakyOrMissing = 4, RefsFromNonUiToUi = 51 };
        var result = DependencyBaseline.Apply(_path, changed, update: false);

        AssertThat(result.Failures.Count).IsEqual(2);
        AssertThat(result.Failures[0])
            .StartsWith("seams_leaky_or_missing is 4, below the baseline 5: " + LoweringInstruction);
        AssertThat(result.Failures[1])
            .IsEqual("refs_from_non_ui_to_ui is 51, above the baseline 50: " +
                     "accepted because seams_leaky_or_missing went down");
        AssertThat(result.AcceptedRises.Count).IsEqual(1);
        AssertThat(result.AcceptedRises[0]).IsEqual(result.Failures[1]);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HigherDecidingNumberFailsWithoutALoweringInstruction()
    {
        Record(BaselineJson);

        var result = DependencyBaseline.Apply(_path,
            Baseline with { UpEdges = 11, RefsFromNonUiToUi = 51 }, update: false);

        AssertThat(result.Failures.Count).IsEqual(2);
        AssertThat(result.Failures[0]).IsEqual("up_edges is 11, above the baseline 10");
        AssertThat(result.Failures[1]).IsEqual("refs_from_non_ui_to_ui is 51, above the baseline 50");
        AssertThat(result.Failures.Any(f => f.Contains(LoweringInstruction, StringComparison.Ordinal))).IsFalse();
        AssertThat(result.AcceptedRises.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SwitchRewritesOnlyWhenTheDecidingNumberWentDown()
    {
        Record(BaselineJson);
        var accepted = Baseline with { SeamsLeakyOrMissing = 4, RefsFromNonUiToUi = 51 };

        var refused = DependencyBaseline.Apply(_path, accepted with { UpEdges = 11 }, update: true);
        AssertThat(refused.Failures.Count).IsEqual(2);
        AssertThat(refused.Failures[0]).IsEqual("up_edges is 11, above the baseline 10");
        AssertThat(File.ReadAllText(_path)).IsEqual(BaselineJson);

        var rewritten = DependencyBaseline.Apply(_path, accepted, update: true);
        AssertThat(rewritten.Failures.Count).IsEqual(0);
        AssertThat(rewritten.AcceptedRises.Count).IsEqual(1);
        AssertThat(rewritten.AcceptedRises[0])
            .IsEqual("refs_from_non_ui_to_ui is 51, above the baseline 50: " +
                     "accepted because seams_leaky_or_missing went down");
        AssertThat(File.ReadAllText(_path)).IsEqual("{\n  \"up_edges\": 10,\n  \"biggest_loop\": 4,\n" +
                                                    "  \"seams_leaky_or_missing\": 4,\n" +
                                                    "  \"refs_from_non_ui_to_ui\": 51\n}\n");
        AssertThat(DependencyBaseline.Read(_path).Numbers!.ToString()).IsEqual(accepted.ToString());
        var after = DependencyBaseline.Apply(_path, accepted, update: false);
        AssertThat(after.Failures.Count).IsEqual(0);
        AssertThat(after.AcceptedRises.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SwitchLeavesTheFileUntouchedWhenNoNumberDiffers()
    {
        Record(BaselineJson);
        var before = File.ReadAllBytes(_path);

        var result = DependencyBaseline.Apply(_path, Baseline, update: true);

        AssertThat(result.Failures.Count).IsEqual(0);
        AssertThat(result.AcceptedRises.Count).IsEqual(0);
        AssertThat(File.ReadAllBytes(_path).SequenceEqual(before)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void IssueTwoFiftyFiveMeasurementsDecideByPriority()
    {
        Record("{\"up_edges\": 33, \"biggest_loop\": 35, \"seams_leaky_or_missing\": 21, " +
               "\"refs_from_non_ui_to_ui\": 148}");

        var fullStep = DependencyBaseline.Apply(_path, new GatedNumbers(33, 35, 18, 143), update: false);
        AssertThat(fullStep.Failures[0])
            .StartsWith("seams_leaky_or_missing is 18, below the baseline 21: " + LoweringInstruction);
        AssertThat(fullStep.AcceptedRises.Count).IsEqual(0);

        var lootHalf = DependencyBaseline.Apply(_path, new GatedNumbers(34, 36, 18, 144), update: false);
        AssertThat(lootHalf.Failures[0]).IsEqual("up_edges is 34, above the baseline 33");
        AssertThat(lootHalf.Failures.Any(f => f.Contains(LoweringInstruction, StringComparison.Ordinal))).IsFalse();
        AssertThat(lootHalf.AcceptedRises.Count).IsEqual(0);

        var previewHalf = DependencyBaseline.Apply(_path, new GatedNumbers(32, 34, 21, 147), update: false);
        AssertThat(previewHalf.Failures.Count).IsEqual(3);
        AssertThat(previewHalf.Failures.All(f => f.Contains(LoweringInstruction, StringComparison.Ordinal)))
            .IsTrue();
        AssertThat(previewHalf.AcceptedRises.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MissingOrMalformedBaselineFailsWithItsPath()
    {
        var missing = DependencyBaseline.Apply(_path, Baseline, update: false);
        AssertThat(missing.Failures.Count).IsEqual(1);
        AssertThat(missing.Failures[0]).IsEqual($"dependency baseline {_path} is missing");

        var missingWithSwitch = DependencyBaseline.Apply(_path, Baseline, update: true);
        AssertThat(missingWithSwitch.Failures.Count).IsEqual(1);
        AssertThat(missingWithSwitch.Failures[0]).IsEqual($"dependency baseline {_path} is missing");
        AssertThat(File.Exists(_path)).IsFalse();

        File.WriteAllText(_path, "{\"up_edges\": 1}");
        var malformed = DependencyBaseline.Apply(_path, Baseline, update: false);
        AssertThat(malformed.Failures.Count).IsEqual(1);
        AssertThat(malformed.Failures[0]).StartsWith($"dependency baseline {_path} is malformed");

        var malformedWithSwitch = DependencyBaseline.Apply(_path, Baseline, update: true);
        AssertThat(malformedWithSwitch.Failures.Count).IsEqual(1);
        AssertThat(malformedWithSwitch.Failures[0]).StartsWith($"dependency baseline {_path} is malformed");
        AssertThat(File.ReadAllText(_path)).IsEqual("{\"up_edges\": 1}");
    }

    private void Record(string json) => File.WriteAllText(_path, json);
}
