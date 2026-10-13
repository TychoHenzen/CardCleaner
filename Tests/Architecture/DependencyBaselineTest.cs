using System;
using System.IO;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture baselines for the ratchet of the dependency gate (#240 AC-5).</summary>
[TestSuite]
public class DependencyBaselineTest
{
    private static readonly GatedNumbers Baseline = new(10, 4, 3, 2, 50);

    private string _path = null!;

    [BeforeTest]
    public void CreatePath() => _path = Path.Combine(Path.GetTempPath(), $"dependency-baseline-{Guid.NewGuid():N}.json");

    [AfterTest]
    public void DeleteFile()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void HigherNumberFailsNamingTheNumber()
    {
        Record();

        var failures = DependencyBaseline.Apply(_path, Baseline with { UpEdges = 11 }, update: false);

        AssertThat(failures.Count).IsEqual(1);
        AssertThat(failures[0]).IsEqual("up_edges is 11, above the baseline 10");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LowerNumberFailsWithTheLoweringCommand()
    {
        Record();

        var failures = DependencyBaseline.Apply(_path, Baseline with { SeamsLeaky = 2 }, update: false);

        AssertThat(failures.Count).IsEqual(1);
        AssertThat(failures[0]).StartsWith("seams_leaky is 2, below the baseline 3: lower the baseline with " +
                                          DependencyBaseline.SwitchVariable + "=1");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SwitchRewritesOnlyWhenNoNumberIsHigher()
    {
        Record();
        var lower = Baseline with { BiggestLoop = 3, RefsFromNonUiToUi = 49 };

        var refused = DependencyBaseline.Apply(_path, lower with { UpEdges = 11 }, update: true);
        AssertThat(refused.Count).IsEqual(1);
        AssertThat(DependencyBaseline.Read(_path).Numbers!.ToString()).IsEqual(Baseline.ToString());

        var accepted = DependencyBaseline.Apply(_path, lower, update: true);
        AssertThat(accepted.Count).IsEqual(0);
        AssertThat(DependencyBaseline.Read(_path).Numbers!.ToString()).IsEqual(lower.ToString());
        AssertThat(DependencyBaseline.Apply(_path, lower, update: false).Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MissingOrMalformedBaselineFailsWithItsPath()
    {
        var missing = DependencyBaseline.Apply(_path, Baseline, update: false);
        AssertThat(missing.Count).IsEqual(1);
        AssertThat(missing[0]).IsEqual($"dependency baseline {_path} is missing");

        File.WriteAllText(_path, "{\"up_edges\": 1}");
        var malformed = DependencyBaseline.Apply(_path, Baseline, update: false);
        AssertThat(malformed.Count).IsEqual(1);
        AssertThat(malformed[0]).StartsWith($"dependency baseline {_path} is malformed");

        var malformedWithSwitch = DependencyBaseline.Apply(_path, Baseline, update: true);
        AssertThat(malformedWithSwitch.Count).IsEqual(1);
        AssertThat(malformedWithSwitch[0]).StartsWith($"dependency baseline {_path} is malformed");
        AssertThat(File.ReadAllText(_path)).IsEqual("{\"up_edges\": 1}");
    }

    private void Record() => AssertThat(DependencyBaseline.Apply(_path, Baseline, update: true).Count).IsEqual(0);
}
