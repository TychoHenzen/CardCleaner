using gdUnit4.addons.gdUnit4.src.dotnet;
using Godot;

namespace CardCleaner.Tests.Addons.TestRunner;

/// <summary>
/// Keeps the discovery-cache patch of the vendored gdUnit4 addon working. The patch reaches the package's internal
/// whole-assembly discovery by reflection, so a package update can take it away; the run would then fall back to the
/// per-script discovery, still correct but 17 s slower, with only one stderr line to show for it. These cases fail
/// instead, and check that the cache gives the package's own answer for a suite, a suite with parameterized cases, a
/// helper class and an abstract base. SuiteDiscoveryEquivalenceTest (Daily) checks every script under res://Tests.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SuiteDiscoveryGuardTest
{
    private static readonly string[] Samples =
    {
        "res://Tests/Addons/TestRunner/SuiteDiscoveryGuardTest.cs",
        "res://Tests/Features/Card/Models/Effects/CardEffectSeedTest.cs",
        "res://Tests/TestUtilities/TestServices.cs",
        "res://Tests/Core/PropertyTesting/PropertyTestBase.cs"
    };

    [TestCase]
    public static void TheCacheAnswersFromOneReadOfTheAssembly()
    {
        foreach (var path in Samples)
        {
            AssertThat(SuiteDiscoveryCache.TestsOf(GD.Load<CSharpScript>(path)))
                .OverrideFailureMessage(
                    $"The gdUnit4 discovery cache left {path} to the per-script discovery "
                    + $"({SuiteDiscoveryCache.FailureReason ?? "no failure recorded"}); restore the discovery patch "
                    + "after a gdUnit4 addon or gdUnit4.api update")
                .IsNotNull();
        }
    }

    [TestCase]
    public static void TheCacheGivesThePackagesOwnTests()
    {
        foreach (var path in Samples)
            AssertThat(DiscoveryComparison.Mismatch(path)).IsNull();
    }
}
