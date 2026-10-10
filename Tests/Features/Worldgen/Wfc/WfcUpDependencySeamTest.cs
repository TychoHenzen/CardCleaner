using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Keeps the WFC solver off Deckbuilder's SimpleMapData and AutoTiling's CompiledTransitionResolver. Files under
/// Scripts/Features/Worldgen/Wfc may name neither class: the grid leaves as plain members of WfcGenerationResult,
/// and callers pass the transition pairs in as data. The check blanks comments and literals, then matches each name
/// as a whole word, so a comment or a string does not count and each real hit is reported as file:line.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcUpDependencySeamTest
{
    private const string WfcRoot = "res://Scripts/Features/Worldgen/Wfc";

    // A Wfc file that must stay in the scan. If the scan stops seeing it, the guard could pass on an empty set.
    private const string ScannedFile = "WfcSolver.cs";

    private static readonly string[] ForbiddenNames = { "SimpleMapData", "CompiledTransitionResolver" };

    private static readonly Regex ForbiddenName = new(
        @"\b(?:" + string.Join("|", ForbiddenNames) + @")\b",
        RegexOptions.Compiled);

    [TestCase]
    [TestCategory("Unit")]
    public static void WfcNamesNeitherSimpleMapDataNorCompiledTransitionResolver()
    {
        var files = SourceScan.CsFiles(WfcRoot);
        AssertThat(files.Count > 0)
            .OverrideFailureMessage($"No .cs files found under {WfcRoot}")
            .IsTrue();
        AssertThat(files.Any(file => Path.GetFileName(file) == ScannedFile))
            .OverrideFailureMessage($"The Wfc scan no longer sees {ScannedFile}, so the guard could pass on nothing")
            .IsTrue();

        var hits = files
            .SelectMany(file => Violations(SourceScan.RelativePath(file), SourceScan.Blanked(file)))
            .ToList();
        AssertThat(hits.Count == 0)
            .OverrideFailureMessage("Wfc names SimpleMapData or CompiledTransitionResolver:\n"
                + string.Join("\n", hits))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ForbiddenNamesInCodeAreReportedAndCommentsAndStringsIgnored()
    {
        const string source = "namespace Sample;\n"
            + "// SimpleMapData named in a comment\n"
            + "var text = \"CompiledTransitionResolver named in a string\";\n"
            + "var map = new SimpleMapData();\n"
            + "var resolver = new CompiledTransitionResolver();\n"
            + "var copy = new SimpleMapDataCopy();\n";

        var hits = Violations("Sample.cs", TestSuiteNamingTest.BlankCommentsAndStrings(source)).ToList();

        AssertThat(hits.ToArray()).IsEqual(new[]
        {
            "Sample.cs:4 SimpleMapData",
            "Sample.cs:5 CompiledTransitionResolver",
        });
    }

    private static IEnumerable<string> Violations(string relative, string blanked)
    {
        foreach (Match match in ForbiddenName.Matches(blanked))
        {
            yield return SourceScan.FormatHit(relative, blanked, match);
        }
    }
}
