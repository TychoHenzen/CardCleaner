using System.Collections.Generic;
using System.Linq;
using gdUnit4.addons.gdUnit4.src.dotnet;
using GdUnit4;
using GdUnit4.Core.Discovery;
using Godot;

namespace CardCleaner.Tests.Addons.TestRunner;

/// <summary>
/// Compares what the patched C# discovery hands gdUnit for a script (the discovery cache, or the package bridge when
/// the cache leaves it) with what the package's own per-script discovery finds.
/// </summary>
internal static class DiscoveryComparison
{
    /// <summary>A description of the difference, or null when both give the same tests in the same order.</summary>
    public static string? Mismatch(string resourcePath)
    {
        var script = GD.Load<CSharpScript>(resourcePath);
        var bridge = Describe(GdUnit4NetApiGodotBridge.DiscoverTestsFromScript(script));
        var patched = Describe(SuiteDiscoveryCache.TestsOf(script) ?? GdUnit4NetApiGodotBridge.DiscoverTestsFromScript(script));
        if (bridge.SequenceEqual(patched))
            return null;
        return $"{resourcePath}: the package found [{string.Join("; ", bridge)}], the patched discovery "
            + $"[{string.Join("; ", patched)}]";
    }

    // Every field GdUnit4CSharpApi.DiscoverTests passes to gdUnit except the id, which is fresh on every call.
    private static List<string> Describe(IEnumerable<TestCaseDescriptor> tests)
        => tests.Select(test => string.Join(
                " | ",
                test.ManagedType,
                test.ManagedMethod,
                test.AttributeIndex,
                test.LineNumber,
                test.CodeFilePath,
                test.RequireRunningGodotEngine,
                test.SimpleName,
                test.FullyQualifiedName,
                test.AssemblyPath))
            .ToList();
}
