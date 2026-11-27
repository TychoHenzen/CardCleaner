using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Core;

/// <summary>
/// Scripts/Core may not depend on the Deckbuilder feature (#476). No file under Scripts/Core may name the
/// Features.Deckbuilder namespace or any type declared under Scripts/Features/Deckbuilder. The tile loading code that
/// used to sit in Scripts/Core/Services lives in Scripts/Features/Deckbuilder/Tiles/Loading, so the scan counts its
/// types as Deckbuilder types.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CoreDeckbuilderBoundaryTest
{
    private const string ScriptsRoot = "res://Scripts";
    private const string DeckbuilderFolder = "Scripts/Features/Deckbuilder/";
    private const string CoreFolder = "Scripts/Core/";

    private static readonly Regex DeckbuilderNamespace = new(@"\bFeatures\.Deckbuilder\b", RegexOptions.Compiled);

    [TestCase]
    public static void CoreFilesNameNoDeckbuilderType()
    {
        var sources = SourceScan.CsFiles(ScriptsRoot)
            .Select(path => (Folder: SourceScan.RelativePath(path), Source: File.ReadAllText(path)))
            .ToList();
        var deckbuilderTypes = sources
            .Where(file => file.Folder.StartsWith(DeckbuilderFolder, StringComparison.Ordinal))
            .SelectMany(file => CoreCardBoundaryTest.DeclaredTypeNames(file.Source))
            .ToList();
        var coreFiles = sources
            .Where(file => file.Folder.StartsWith(CoreFolder, StringComparison.Ordinal))
            .ToList();
        var violations = coreFiles
            .SelectMany(file => DeckbuilderViolations(file.Folder, file.Source, deckbuilderTypes));

        // A scan that found no Core file or no Deckbuilder type passes trivially, so the guard must have seen both.
        // The tile loading types left Core, so the scan must now count them as Deckbuilder types.
        AssertThat(coreFiles).IsNotEmpty();
        AssertThat(deckbuilderTypes).Contains("GridMapGenerator", "TileRegistry", "TileDataLoader");

        // Joined into one string so a failure names every offending file, not just how many there are.
        AssertThat(string.Join(System.Environment.NewLine, violations)).IsEmpty();
    }

    [TestCase]
    public static void UsingTheDeckbuilderNamespaceInCoreIsReported()
    {
        const string source = "using CardCleaner.Scripts.Features.Deckbuilder.Tiles;\npublic class Helper {}\n";

        AssertThat(DeckbuilderViolations("Core/Helper.cs", source, new List<string>()).ToList()).HasSize(1);
    }

    [TestCase]
    public static void NamingADeckbuilderTypeInCoreIsReported()
    {
        const string source = "public class Helper\n{\n    private TileRegistry? _registry;\n}\n";

        var violations = DeckbuilderViolations("Core/Helper.cs", source, new List<string> { "TileRegistry" }).ToList();

        AssertThat(violations).HasSize(1);
        AssertThat(violations[0]).IsEqual("Core/Helper.cs names the Deckbuilder type TileRegistry");
    }

    [TestCase]
    public static void CommentsAndStringsMentioningDeckbuilderTypesAreIgnored()
    {
        const string source = "// TileRegistry and Features.Deckbuilder are described here\n"
            + "var text = \"TileRegistry\";\n";

        var violations = DeckbuilderViolations("Core/Helper.cs", source, new List<string> { "TileRegistry" });

        AssertThat(violations.ToList()).IsEmpty();
    }

    internal static IEnumerable<string> DeckbuilderViolations(
        string folder, string source, IEnumerable<string> deckbuilderTypes)
    {
        var code = TestSuiteNamingTest.BlankCommentsAndStrings(source);
        if (DeckbuilderNamespace.IsMatch(code))
            yield return $"{folder} names the Features.Deckbuilder namespace";

        var names = deckbuilderTypes.Distinct().ToList();
        if (names.Count == 0)
            yield break;

        var typeUse = new Regex(@"\b(?:" + string.Join("|", names.Select(Regex.Escape)) + @")\b");
        foreach (var name in typeUse.Matches(code).Select(match => match.Value).Distinct())
            yield return $"{folder} names the Deckbuilder type {name}";
    }
}
