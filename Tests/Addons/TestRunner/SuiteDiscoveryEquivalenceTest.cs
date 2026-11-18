using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Addons.TestRunner;

/// <summary>
/// The discovery-cache patch gives gdUnit the same tests as the package's per-script discovery for every C# script
/// under res://Tests: same classes, methods, case indexes, names, lines and paths, in the same order. A Daily suite:
/// asking the package for each of about 500 scripts takes about 17 s, which is the cost the cache removes from every
/// run. SuiteDiscoveryGuardTest checks a sample on every pull request.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
[TestCategory("Daily")]
public class SuiteDiscoveryEquivalenceTest
{
    [TestCase(Timeout = 120000)]
    public static void EveryTestScriptGetsThePackagesOwnTests()
    {
        var scripts = new List<string>();
        Collect("res://Tests", scripts);
        AssertThat(scripts.Count > 300)
            .OverrideFailureMessage($"Only {scripts.Count} C# scripts found under res://Tests, so the check would pass on little")
            .IsTrue();

        var mismatches = scripts.Select(DiscoveryComparison.Mismatch).OfType<string>();

        AssertThat(string.Join("\n", mismatches)).IsEmpty();
    }

    private static void Collect(string directory, List<string> scripts)
    {
        foreach (var child in DirAccess.GetDirectoriesAt(directory))
            Collect($"{directory}/{child}", scripts);
        scripts.AddRange(DirAccess.GetFilesAt(directory)
            .Where(file => file.EndsWith(".cs", System.StringComparison.Ordinal))
            .Select(file => $"{directory}/{file}"));
    }
}
