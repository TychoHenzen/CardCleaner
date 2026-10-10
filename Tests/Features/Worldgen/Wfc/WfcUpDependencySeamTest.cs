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
/// The tile catalog gets the same check: WFC reads tile data only through IWfcTileCatalog, so Wfc names none of the
/// catalog's types and imports none of the catalog's namespaces.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcUpDependencySeamTest
{
    private const string WfcRoot = "res://Scripts/Features/Worldgen/Wfc";

    // A Wfc file that must stay in the scan. If the scan stops seeing it, the guard could pass on an empty set.
    private const string ScannedFile = "WfcSolver.cs";

    // The port that stands in for the catalog. The catalog guard also checks that the scan still reads this
    // declaration as code, so a missing or blanked file cannot make it pass.
    private const string CatalogPortFile = "IWfcTileCatalog.cs";
    private const string CatalogPortDeclaration = "interface IWfcTileCatalog";

    private static readonly string[] ForbiddenNames = { "SimpleMapData", "CompiledTransitionResolver" };

    private static readonly Regex ForbiddenName = new(
        @"\b(?:" + string.Join("|", ForbiddenNames) + @")\b",
        RegexOptions.Compiled);

    // The tile-catalog types, by simple name.
    private static readonly string[] ForbiddenCatalogNames =
    {
        "ITileRegistry", "ITileMetadataProvider", "TileRegistry", "TileDefinition", "TileDefinitionOptions",
        "VariationGroup", "VariantWeight", "VariationMode", "AutoTileFormatDefinition", "AutoTileFormatRegistry",
        "VariantDefinition", "TilesetConfig",
    };

    // The catalog namespaces, by tail, so the CardCleaner.Scripts and CardCleaner spellings both match, and so does a
    // partly qualified reference such as Features.Deckbuilder.Tiles.TileDefinition.
    private static readonly string[] ForbiddenCatalogNamespaces =
    {
        "Deckbuilder.Tiles", "Core.Services", "Features.Worldgen.AutoTiling",
    };

    private static readonly Regex ForbiddenCatalogReference = new(
        @"\b(?:" + string.Join("|", ForbiddenCatalogNames.Concat(ForbiddenCatalogNamespaces).Select(Regex.Escape))
            + @")\b",
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
            .SelectMany(file => Violations(SourceScan.RelativePath(file), SourceScan.Blanked(file), ForbiddenName))
            .ToList();
        AssertThat(hits.Count == 0)
            .OverrideFailureMessage("Wfc names SimpleMapData or CompiledTransitionResolver:\n"
                + string.Join("\n", hits))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void WfcNamesNoTileCatalogTypeAndImportsNoCatalogNamespace()
    {
        var files = SourceScan.CsFiles(WfcRoot);
        AssertThat(files.Count > 0)
            .OverrideFailureMessage($"No .cs files found under {WfcRoot}")
            .IsTrue();
        var catalogPort = files.FirstOrDefault(file => Path.GetFileName(file) == CatalogPortFile);
        AssertThat(catalogPort != null && SourceScan.Blanked(catalogPort).Contains(CatalogPortDeclaration))
            .OverrideFailureMessage($"The Wfc scan no longer reads {CatalogPortFile} as code, so the catalog guard "
                + "could pass on nothing")
            .IsTrue();

        var hits = files
            .SelectMany(file => Violations(SourceScan.RelativePath(file), SourceScan.Blanked(file),
                ForbiddenCatalogReference))
            .ToList();
        AssertThat(hits.Count == 0)
            .OverrideFailureMessage("Wfc names a tile-catalog type or imports a catalog namespace; read tile data "
                + "through IWfcTileCatalog instead:\n" + string.Join("\n", hits))
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

        var hits = Violations("Sample.cs", TestSuiteNamingTest.BlankCommentsAndStrings(source), ForbiddenName).ToList();

        AssertThat(hits.ToArray()).IsEqual(new[]
        {
            "Sample.cs:4 SimpleMapData",
            "Sample.cs:5 CompiledTransitionResolver",
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CatalogNamesAndNamespacesAreReportedAndNearMissesIgnored()
    {
        const string source = "namespace Sample;\n"
            + "// ITileRegistry named in a comment\n"
            + "var text = \"Core.Services named in a string\";\n"
            + "using Catalog = CardCleaner.Scripts.Core.Services;\n"
            + "ITileRegistry registry = null!;\n"
            + "var copy = new TileRegistryWfcCatalog(registry);\n"
            + "var port = new IWfcTileCatalog();\n"
            + "var probe = default(Features.Deckbuilder.Tiles.TileDefinition);\n";

        var hits = Violations("Sample.cs", TestSuiteNamingTest.BlankCommentsAndStrings(source),
            ForbiddenCatalogReference).ToList();

        AssertThat(hits.ToArray()).IsEqual(new[]
        {
            "Sample.cs:4 Core.Services",
            "Sample.cs:5 ITileRegistry",
            "Sample.cs:8 Deckbuilder.Tiles",
            "Sample.cs:8 TileDefinition",
        });
    }

    private static IEnumerable<string> Violations(string relative, string blanked, Regex pattern)
    {
        foreach (Match match in pattern.Matches(blanked))
        {
            yield return SourceScan.FormatHit(relative, blanked, match);
        }
    }
}
