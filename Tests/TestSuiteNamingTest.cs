using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Tests;

/// <summary>
/// gdUnit finds a C# suite by its file name, so a suite class named differently from its file is skipped without an
/// error, and an abstract one never runs. Every suite under Tests/ must be a concrete class named after its file.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TestSuiteNamingTest
{
    private const string TestsRoot = "res://Tests";

    // Only an attribute at the start of a line counts. The attribute may be spelled [TestSuite], [TestSuite()],
    // [TestSuiteAttribute] or [GdUnit4.TestSuite]. Keep this text identical to $SuiteAttributePattern in
    // Scripts/ci/Run-GdUnit.ps1; Test-RunGdUnit.ps1 fails when the two copies differ.
    internal const string SuiteAttributePattern = @"^\s*\[(?:GdUnit4\.)?TestSuite(?:Attribute)?(?:\(\s*\))?\]";

    // Comments and literals as one alternation, tried left to right, so a "/*" inside a string is string text. Covers
    // line and block comments, raw, verbatim and regular strings, and char literals. Each match is blanked before suite
    // attributes are matched. Known gaps: a string nested in an interpolation hole, and #if false blocks. Keep this text
    // identical to $CodeNoisePattern in Scripts/ci/Run-GdUnit.ps1.
    internal const string CodeNoisePattern = @"//[^\n]*|/\*[\s\S]*?\*/|(""{3,})[\s\S]*?\1|@""(?:[^""]|"""")*""|""(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)+'";

    // \x7B is the opening brace, escaped so brace-counting tools still see balanced braces in this file.
    private static readonly Regex SuiteDeclaration = new(
        SuiteAttributePattern + @"(?<between>[^\x7B;]*?)\bclass\s+(?<name>\w+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex CodeNoise = new(CodeNoisePattern, RegexOptions.Compiled);

    private static readonly Regex NonNewline = new(@"[^\r\n]", RegexOptions.Compiled);

    private static readonly Regex AbstractModifier = new(@"\babstract\b", RegexOptions.Compiled);

    /// <summary>The source with every comment and string literal blanked to spaces; newlines are kept so line numbers hold.</summary>
    public static string BlankCommentsAndStrings(string source)
    {
        return CodeNoise.Replace(source, match => NonNewline.Replace(match.Value, " "));
    }

    /// <summary>Every reason gdUnit would skip a suite declared in <paramref name="source" />, one entry per problem.</summary>
    public static IEnumerable<string> SkippedSuites(string fileName, string source)
    {
        var expected = Path.GetFileNameWithoutExtension(fileName);
        foreach (Match declaration in SuiteDeclaration.Matches(BlankCommentsAndStrings(source)))
        {
            var name = declaration.Groups["name"].Value;
            if (name != expected)
                yield return $"{fileName} declares suite class {name}; gdUnit only finds {expected}";
            if (AbstractModifier.IsMatch(declaration.Groups["between"].Value))
                yield return $"{fileName} marks the abstract class {name} as a suite; it never runs";
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EverySuiteUnderTestsIsAConcreteClassNamedAfterItsFile()
    {
        var root = ProjectSettings.GlobalizePath(TestsRoot);

        var skipped = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => SkippedSuites(Path.GetFileName(path), File.ReadAllText(path)));

        // Joined into one string so a failure names every offending file, not just how many there are.
        AssertThat(string.Join(System.Environment.NewLine, skipped)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SuiteClassNamedDifferentlyFromItsFileIsReported()
    {
        const string source = "[TestSuite]\npublic class EleAspectsEnhancedTest\n{\n}\n";

        var diagnostics = SkippedSuites("EleAspectsTest.cs", source).ToList();

        AssertThat(diagnostics).HasSize(1);
        AssertThat(diagnostics[0]).IsEqual(
            "EleAspectsTest.cs declares suite class EleAspectsEnhancedTest; gdUnit only finds EleAspectsTest");
    }

    [TestCase("[TestSuite()]")]
    [TestCase("[TestSuiteAttribute]")]
    [TestCase("[GdUnit4.TestSuite]")]
    [TestCategory("Unit")]
    public static void EverySpellingOfTheSuiteAttributeIsRecognized(string attribute)
    {
        var source = attribute + "\npublic class EleAspectsEnhancedTest\n{\n}\n";

        AssertThat(SkippedSuites("EleAspectsTest.cs", source).ToList()).HasSize(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AbstractSuiteClassIsReported()
    {
        const string source = "[TestSuite]\n[RequireGodotRuntime]\npublic abstract class PropertyTestBase\n{\n}\n";

        AssertThat(SkippedSuites("PropertyTestBase.cs", source).ToList()).HasSize(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ConcreteSuiteNamedAfterItsFileIsAccepted()
    {
        const string source = "[TestSuite]\n[RequireGodotRuntime]\npublic partial class GoodTest : PropertyTestBase\n{\n}\n";

        AssertThat(SkippedSuites("GoodTest.cs", source).ToList()).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MentionOfTheAttributeInACommentIsIgnored()
    {
        const string source = "/// A [TestSuite] class named after its file.\npublic static class Helper\n{\n}\n";

        AssertThat(SkippedSuites("Helpers.cs", source).ToList()).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SuiteAttributeInBlockCommentIsIgnored()
    {
        const string source = "/*\n[TestSuite]\npublic class EleAspectsEnhancedTest\n{\n}\n*/\n";

        AssertThat(SkippedSuites("EleAspectsTest.cs", source).ToList()).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SuiteAttributeInVerbatimStringIsIgnored()
    {
        const string source = "var text = @\"\n[TestSuite]\npublic class EleAspectsEnhancedTest\n{\n}\n\";\n";

        AssertThat(SkippedSuites("EleAspectsTest.cs", source).ToList()).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SuiteAttributeInRawStringIsIgnored()
    {
        const string source = "var text = \"\"\"\n[TestSuite]\npublic class EleAspectsEnhancedTest\n{\n}\n\"\"\";\n";

        AssertThat(SkippedSuites("EleAspectsTest.cs", source).ToList()).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SuiteAfterCommentAndStringNoiseIsStillFound()
    {
        const string source = "/* note */\nvar quote = \"/*\";\n[TestSuite]\npublic class EleAspectsEnhancedTest\n{\n}\n";

        AssertThat(SkippedSuites("EleAspectsTest.cs", source).ToList()).HasSize(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BlankingKeepsLineCount()
    {
        const string source = "a /* x\ny */ \"s\"\n[TestSuite]\n";

        var blanked = BlankCommentsAndStrings(source);

        AssertThat(blanked.Length).IsEqual(source.Length);
        AssertThat(blanked.Count(c => c == '\n')).IsEqual(source.Count(c => c == '\n'));
        AssertThat(blanked.Split('\n')[2]).IsEqual("[TestSuite]");
    }
}
