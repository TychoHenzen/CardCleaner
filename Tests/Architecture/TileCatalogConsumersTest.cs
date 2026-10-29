using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Architecture;

/// <summary>
/// The map generators read tiles through ITileCatalog, not the concrete TileCatalogData, so the Deckbuilder
/// generators and the mesh terrain code name only the interface. Only the composition root builds a TileCatalogData.
/// The check blanks comments and literals, then matches the name as a whole word, so a doc comment that mentions the
/// class does not count and each real hit is reported as file:line.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileCatalogConsumersTest
{
    private const string GeneratorRoot = "res://Scripts/Features/Deckbuilder/Services";
    private const string MeshRoot = "res://Scripts/Features/Worldgen/IrregularMesh";

    // Files that must stay in the scan. If the scan stops seeing them, the guard could pass on an empty set.
    private static readonly string[] ScannedFiles = { "GridMapGenerator.cs", "MeshMapGenerator.cs" };

    private static readonly Regex ConcreteCatalog = new(@"\bTileCatalogData\b", RegexOptions.Compiled);

    [TestCase]
    [TestCategory("Unit")]
    public static void MapGeneratorsNameNoConcreteTileCatalog()
    {
        var files = SourceScan.CsFiles(GeneratorRoot).Concat(SourceScan.CsFiles(MeshRoot)).ToList();
        AssertThat(files.Count > 0)
            .OverrideFailureMessage($"No .cs files found under {GeneratorRoot} or {MeshRoot}")
            .IsTrue();
        foreach (var name in ScannedFiles)
        {
            AssertThat(files.Any(file => Path.GetFileName(file) == name))
                .OverrideFailureMessage(
                    $"The tile catalog scan no longer sees {name}, so the guard could pass on nothing")
                .IsTrue();
        }

        var hits = files
            .SelectMany(file => Violations(SourceScan.RelativePath(file), SourceScan.Blanked(file)))
            .ToList();
        AssertThat(hits.Count == 0)
            .OverrideFailureMessage("A map generator names TileCatalogData; take ITileCatalog instead, and build the "
                + "catalog only in CompositionRoot:\n" + string.Join("\n", hits))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ConcreteCatalogNameIsReportedAndCommentsAndStringsIgnored()
    {
        const string source = "namespace Sample;\n"
            + "// TileCatalogData named in a comment\n"
            + "var text = \"TileCatalogData named in a string\";\n"
            + "ITileCatalog tiles = null!;\n"
            + "var concrete = new TileCatalogData(null!, null!);\n"
            + "var copy = new TileCatalogDataCopy();\n";

        var hits = Violations("Sample.cs", TestSuiteNamingTest.BlankCommentsAndStrings(source)).ToList();

        AssertThat(hits.ToArray()).IsEqual(new[]
        {
            "Sample.cs:5 TileCatalogData",
        });
    }

    private static IEnumerable<string> Violations(string relative, string blanked)
    {
        foreach (Match match in ConcreteCatalog.Matches(blanked))
        {
            yield return SourceScan.FormatHit(relative, blanked, match);
        }
    }
}
