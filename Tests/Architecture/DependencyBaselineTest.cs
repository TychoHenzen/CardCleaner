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

        var result = DependencyBaseline.Apply(_path, Baseline, Lists(), update: false);

        AssertThat(result.Failures.Count).IsEqual(0);
        AssertThat(result.AcceptedRises.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HigherNumberFailsNamingTheNumber()
    {
        Record(BaselineJson);

        var result = DependencyBaseline.Apply(_path, Baseline with { UpEdges = 11 }, Lists(), update: false);

        AssertThat(result.Failures.Count).IsEqual(1);
        AssertThat(result.Failures[0]).IsEqual("up_edges is 11, above the baseline 10");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LowerNumberFailsWithTheLoweringCommand()
    {
        Record(BaselineJson);

        var result = DependencyBaseline.Apply(_path, Baseline with { SeamsLeakyOrMissing = 4 }, Lists(), update: false);

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
        var result = DependencyBaseline.Apply(_path, changed, Lists(), update: false);

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
            Baseline with { UpEdges = 11, RefsFromNonUiToUi = 51 }, Lists(), update: false);

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

        var refused = DependencyBaseline.Apply(_path, accepted with { UpEdges = 11 }, Lists(), update: true);
        AssertThat(refused.Failures.Count).IsEqual(2);
        AssertThat(refused.Failures[0]).IsEqual("up_edges is 11, above the baseline 10");
        AssertThat(File.ReadAllText(_path)).IsEqual(BaselineJson);

        var rewritten = DependencyBaseline.Apply(_path, accepted, Lists(), update: true);
        AssertThat(rewritten.Failures.Count).IsEqual(0);
        AssertThat(rewritten.AcceptedRises.Count).IsEqual(1);
        AssertThat(rewritten.AcceptedRises[0])
            .IsEqual("refs_from_non_ui_to_ui is 51, above the baseline 50: " +
                     "accepted because seams_leaky_or_missing went down");
        AssertThat(File.ReadAllText(_path)).IsEqual(
            "{\n  \"up_edges\": 10,\n  \"biggest_loop\": 4,\n" +
            "  \"seams_leaky_or_missing\": 4,\n  \"refs_from_non_ui_to_ui\": 51,\n" +
            "  \"up_edge_pairs\": [],\n  \"biggest_loop_classes\": [],\n" +
            "  \"seam_classes\": [],\n  \"ui_ref_pairs\": []\n}\n");
        AssertThat(DependencyBaseline.Read(_path).Numbers!.ToString()).IsEqual(accepted.ToString());
        var after = DependencyBaseline.Apply(_path, accepted, Lists(), update: false);
        AssertThat(after.Failures.Count).IsEqual(0);
        AssertThat(after.AcceptedRises.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SwitchLeavesTheFileUntouchedWhenNoNumberDiffers()
    {
        DependencyBaseline.Write(_path, Baseline, Lists("A -> B"));
        var before = File.ReadAllBytes(_path);

        var result = DependencyBaseline.Apply(_path, Baseline, Lists("A -> B"), update: true);

        AssertThat(result.Failures.Count).IsEqual(0);
        AssertThat(result.AcceptedRises.Count).IsEqual(0);
        AssertThat(File.ReadAllBytes(_path).SequenceEqual(before)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SwitchRecordsListsWhenNumbersAreEqual()
    {
        Record(BaselineJson);

        var result = DependencyBaseline.Apply(_path, Baseline, Lists("A -> B"), update: true);

        AssertThat(result.Failures.Count).IsEqual(0);
        var read = DependencyBaseline.Read(_path);
        AssertThat(read.Lists is not null).IsTrue();
        AssertThat(string.Join(",", read.Lists!.UpEdgePairs)).IsEqual("A -> B");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SwitchRecordsAChangedListWhenNumbersAreEqual()
    {
        DependencyBaseline.Write(_path, Baseline, Lists("A -> B"));

        var result = DependencyBaseline.Apply(_path, Baseline, Lists("C -> D"), update: true);

        AssertThat(result.Failures.Count).IsEqual(0);
        AssertThat(string.Join(",", DependencyBaseline.Read(_path).Lists!.UpEdgePairs)).IsEqual("C -> D");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SwitchNeverWritesWhenTheDecidingNumberRose()
    {
        Record(BaselineJson);
        var before = File.ReadAllBytes(_path);

        var result = DependencyBaseline.Apply(_path, Baseline with { UpEdges = 11 }, Lists("A -> B"), update: true);

        AssertThat(result.Failures.Count).IsEqual(1);
        AssertThat(result.Failures[0]).IsEqual("up_edges is 11, above the baseline 10");
        AssertThat(File.ReadAllBytes(_path).SequenceEqual(before)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void IssueTwoFiftyFiveMeasurementsDecideByPriority()
    {
        Record("{\"up_edges\": 33, \"biggest_loop\": 35, \"seams_leaky_or_missing\": 21, " +
               "\"refs_from_non_ui_to_ui\": 148}");

        var fullStep = DependencyBaseline.Apply(_path, new GatedNumbers(33, 35, 18, 143), Lists(), update: false);
        AssertThat(fullStep.Failures[0])
            .StartsWith("seams_leaky_or_missing is 18, below the baseline 21: " + LoweringInstruction);
        AssertThat(fullStep.AcceptedRises.Count).IsEqual(0);

        var lootHalf = DependencyBaseline.Apply(_path, new GatedNumbers(34, 36, 18, 144), Lists(), update: false);
        AssertThat(lootHalf.Failures[0]).IsEqual("up_edges is 34, above the baseline 33");
        AssertThat(lootHalf.Failures.Any(f => f.Contains(LoweringInstruction, StringComparison.Ordinal))).IsFalse();
        AssertThat(lootHalf.AcceptedRises.Count).IsEqual(0);

        var previewHalf = DependencyBaseline.Apply(_path, new GatedNumbers(32, 34, 21, 147), Lists(), update: false);
        AssertThat(previewHalf.Failures.Count).IsEqual(3);
        AssertThat(previewHalf.Failures.All(f => f.Contains(LoweringInstruction, StringComparison.Ordinal)))
            .IsTrue();
        AssertThat(previewHalf.AcceptedRises.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MissingOrMalformedBaselineFailsWithItsPath()
    {
        var missing = DependencyBaseline.Apply(_path, Baseline, Lists(), update: false);
        AssertThat(missing.Failures.Count).IsEqual(1);
        AssertThat(missing.Failures[0]).IsEqual($"dependency baseline {_path} is missing");

        var missingWithSwitch = DependencyBaseline.Apply(_path, Baseline, Lists(), update: true);
        AssertThat(missingWithSwitch.Failures.Count).IsEqual(1);
        AssertThat(missingWithSwitch.Failures[0]).IsEqual($"dependency baseline {_path} is missing");
        AssertThat(File.Exists(_path)).IsFalse();

        File.WriteAllText(_path, "{\"up_edges\": 1}");
        var malformed = DependencyBaseline.Apply(_path, Baseline, Lists(), update: false);
        AssertThat(malformed.Failures.Count).IsEqual(1);
        AssertThat(malformed.Failures[0]).StartsWith($"dependency baseline {_path} is malformed");

        var malformedWithSwitch = DependencyBaseline.Apply(_path, Baseline, Lists(), update: true);
        AssertThat(malformedWithSwitch.Failures.Count).IsEqual(1);
        AssertThat(malformedWithSwitch.Failures[0]).StartsWith($"dependency baseline {_path} is malformed");
        AssertThat(File.ReadAllText(_path)).IsEqual("{\"up_edges\": 1}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WriteThenReadKeepsNumbersAndSortedLists()
    {
        DependencyBaseline.Write(_path, Baseline, Lists("B", "A"));

        var read = DependencyBaseline.Read(_path);

        AssertThat(read.Failure).IsNull();
        AssertThat(read.Numbers!.ToString()).IsEqual(Baseline.ToString());
        AssertThat(string.Join(",", read.Lists!.UpEdgePairs)).IsEqual("A,B");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FourKeyFileStillReadsWithoutLists()
    {
        Record(BaselineJson);

        var read = DependencyBaseline.Read(_path);

        AssertThat(read.Failure).IsNull();
        AssertThat(read.Numbers is not null).IsTrue();
        AssertThat(read.Lists is null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PartialOrNonArrayListsAreMalformed()
    {
        Record(BaselineJson[..^1] + ", \"up_edge_pairs\": []}");
        AssertThat(DependencyBaseline.Read(_path).Failure)
            .IsEqual($"dependency baseline {_path} is malformed: no list biggest_loop_classes");

        Record(BaselineJson[..^1] + ", \"up_edge_pairs\": [], \"biggest_loop_classes\": [], " +
               "\"seam_classes\": 5, \"ui_ref_pairs\": []}");
        AssertThat(DependencyBaseline.Read(_path).Failure)
            .IsEqual($"dependency baseline {_path} is malformed: list seam_classes is not an array of strings");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RefusedRiseNamesJoinedAndLeftClasses()
    {
        DependencyBaseline.Write(_path, Baseline, Lists("A -> B", "C -> D"));

        var result = DependencyBaseline.Apply(_path, new GatedNumbers(11, 4, 5, 50), Lists("A -> B", "E -> F"),
            update: false);

        AssertThat(result.Failures.Count).IsEqual(1);
        AssertThat(result.Failures[0])
            .IsEqual("up_edges is 11, above the baseline 10; up_edge_pairs joined: E -> F; left: C -> D");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AcceptedRiseNamesJoinedAndLeftClasses()
    {
        DependencyBaseline.Write(_path, Baseline, Lists());

        var result = DependencyBaseline.Apply(_path, new GatedNumbers(10, 4, 4, 51),
            new NamedLists([], [], [], ["X -> Y"]), update: false);

        AssertThat(result.Failures.Count).IsEqual(2);
        AssertThat(result.Failures[1]).IsEqual("refs_from_non_ui_to_ui is 51, above the baseline 50: " +
                                               "accepted because seams_leaky_or_missing went down; " +
                                               "ui_ref_pairs joined: X -> Y; left: none");
        AssertThat(result.AcceptedRises[0]).IsEqual(result.Failures[1]);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ListOnlyDifferenceDoesNotFailButIsNoted()
    {
        DependencyBaseline.Write(_path, Baseline, new NamedLists([], [], ["S1"], []));

        var result = DependencyBaseline.Apply(_path, Baseline, new NamedLists([], [], ["S2"], []), update: false);

        AssertThat(result.Failures.Count).IsEqual(0);
        AssertThat(result.ListNotes.Count).IsEqual(1);
        AssertThat(result.ListNotes[0]).IsEqual("seam_classes joined: S2; left: S1");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BaselineWithoutListsNotesIt()
    {
        Record(BaselineJson);

        var result = DependencyBaseline.Apply(_path, Baseline, Lists(), update: false);

        AssertThat(result.Failures.Count).IsEqual(0);
        AssertThat(result.ListNotes.Count).IsEqual(1);
        AssertThat(result.ListNotes[0])
            .IsEqual("baseline records no lists; run the update command to record them");
    }

    private static NamedLists Lists(params string[] upEdgePairs) => new(upEdgePairs, [], [], []);

    private void Record(string json) => File.WriteAllText(_path, json);
}
