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

    // Only an attribute at the start of a line counts, so comments and string literals that mention it do not.
    // \x7B is the opening brace, escaped so brace-counting tools still see balanced braces in this file.
    private static readonly Regex SuiteDeclaration = new(
        @"^\s*\[TestSuite\](?<between>[^\x7B;]*?)\bclass\s+(?<name>\w+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex AbstractModifier = new(@"\babstract\b", RegexOptions.Compiled);

    /// <summary>Every reason gdUnit would skip a suite declared in <paramref name="source" />, one entry per problem.</summary>
    public static IEnumerable<string> SkippedSuites(string fileName, string source)
    {
        var expected = Path.GetFileNameWithoutExtension(fileName);
        foreach (Match declaration in SuiteDeclaration.Matches(source))
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
}
