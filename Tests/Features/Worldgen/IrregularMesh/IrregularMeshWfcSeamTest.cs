using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Keeps Irregular Map on the WFC front door. Files under Scripts/Features/Worldgen/IrregularMesh may name only
/// IWfcTerrainSolver, WfcGraphSolution and WfcGenerationResult from Scripts/Features/Worldgen/Wfc. Every other type
/// declared there is a solver internal, and no Wfc sub-namespace may be imported. The check blanks comments and
/// literals, then matches names in the text. It is a name match, not a symbol match, so a same-named type declared
/// elsewhere is reported as well. Ambiguous names fail closed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class IrregularMeshWfcSeamTest
{
    private const string WfcRoot = "res://Scripts/Features/Worldgen/Wfc";
    private const string IrregularMeshRoot = "res://Scripts/Features/Worldgen/IrregularMesh";
    private const string ProjectRoot = "res://";

    // A solver internal that must stay declared under Wfc. If the scan stops seeing it, the forbidden set is empty.
    private const string SolverInternal = "WfcSolver";

    private static readonly string[] AllowedNames = { "IWfcTerrainSolver", "WfcGraphSolution", "WfcGenerationResult" };

    // ASSUMPTION: every type declaration starts its own line after optional attributes and modifiers, as this repo's
    // C# style does. A regex finds declarations without a parser, so a declaration written another way is missed.
    private static readonly Regex DeclarationPattern = new(
        @"^[ \t]*(?:\[[^\]\r\n]*\][ \t]*)*"
        + @"(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|file|ref|unsafe|new)\s+)*"
        + @"(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(?<name>[A-Za-z_]\w*)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // The name is the identifier before the parameter list. The ordinary return type holds no ( ; = { }, so an
    // anonymous method at a line start is no declaration. A top-level tuple return type has its own branch, and a
    // tuple nested in another type is not matched.
    private static readonly Regex DelegatePattern = new(
        @"^[ \t]*(?:\[[^\]\r\n]*\][ \t]*)*"
        + @"(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|file|ref|unsafe|new)\s+)*"
        + @"delegate\s+(?:\([^()\r\n]*\)\s+|[^(;={}\r\n]*?\b)(?<name>[A-Za-z_]\w*)(?:<[^>()\r\n]*>)?\s*\(",
        RegexOptions.Multiline | RegexOptions.Compiled);

    // A Wfc sub-namespace reached by a using directive, an alias or a qualified name. The root namespace and the
    // allowed names do not match.
    private static readonly Regex SubNamespaceReference = new(
        @"\bCardCleaner\.Scripts\.Features\.Worldgen\.Wfc\."
        + @"(?!(?:IWfcTerrainSolver|WfcGraphSolution|WfcGenerationResult)\b)[A-Za-z_][\w.]*",
        RegexOptions.Compiled);

    [TestCase]
    [TestCategory("Unit")]
    public static void IrregularMapNamesOnlyTheWfcFrontDoor()
    {
        var declared = DeclaredWfcNames();
        foreach (var allowed in AllowedNames)
        {
            AssertThat(declared.Contains(allowed))
                .OverrideFailureMessage($"Wfc no longer declares {allowed}, so the front door moved or was renamed")
                .IsTrue();
        }

        AssertThat(declared.Contains(SolverInternal))
            .OverrideFailureMessage($"The Wfc scan no longer sees {SolverInternal}, so the forbidden set is empty")
            .IsTrue();

        var forbidden = declared
            .Where(name => !AllowedNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var nameMatch = new Regex(@"\b(?:" + string.Join("|", forbidden.Select(Regex.Escape)) + @")\b");

        var files = CsFiles(IrregularMeshRoot);
        AssertThat(files.Count > 0)
            .OverrideFailureMessage($"No .cs files found under {IrregularMeshRoot}")
            .IsTrue();

        var hits = files.SelectMany(file => Violations(file, nameMatch)).ToList();
        AssertThat(hits.Count == 0)
            .OverrideFailureMessage("Irregular Map names Wfc types outside the front door:\n" + string.Join("\n", hits))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DelegateDeclarationsAreCollectedAndAnonymousMethodsIgnored()
    {
        const string source = "namespace Sample;\n"
            + "public delegate void SolverCallback(int id);\n"
            + "public delegate Func<int, string> Builder(int count);\n"
            + "internal delegate T Selector<T>(T value);\n"
            + "public delegate (int Count, string Label) PairMaker(int id);\n"
            + "delegate { return Math.Max(1, 2); }\n";

        var names = DeclaredNames(source);

        AssertThat(names.OrderBy(name => name, StringComparer.Ordinal).ToArray())
            .IsEqual(new[] { "Builder", "PairMaker", "Selector", "SolverCallback" });
    }

    private static HashSet<string> DeclaredWfcNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in CsFiles(WfcRoot))
        {
            names.UnionWith(DeclaredNames(File.ReadAllText(file)));
        }

        return names;
    }

    private static HashSet<string> DeclaredNames(string source)
    {
        var blanked = TestSuiteNamingTest.BlankCommentsAndStrings(source);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match declaration in DeclarationPattern.Matches(blanked).Concat(DelegatePattern.Matches(blanked)))
        {
            names.Add(declaration.Groups["name"].Value);
        }

        return names;
    }

    // ASSUMPTION: the .cs sources are on disk when the suite runs (editor, headless run or CI checkout). An export
    // without them finds no files and fails the file-count check instead of passing.
    private static List<string> CsFiles(string resRoot)
    {
        return Directory.EnumerateFiles(ProjectSettings.GlobalizePath(resRoot), "*.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetExtension(file) == ".cs")
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> Violations(string osPath, Regex nameMatch)
    {
        var blanked = Blanked(osPath);
        var relative = Path.GetRelativePath(ProjectSettings.GlobalizePath(ProjectRoot), osPath).Replace('\\', '/');
        var matches = nameMatch.Matches(blanked)
            .Concat(SubNamespaceReference.Matches(blanked))
            .OrderBy(match => match.Index);
        foreach (var match in matches)
        {
            yield return FormattableString.Invariant($"{relative}:{LineOf(blanked, match.Index)} {match.Value}");
        }
    }

    private static string Blanked(string osPath) =>
        TestSuiteNamingTest.BlankCommentsAndStrings(File.ReadAllText(osPath));

    private static int LineOf(string text, int index) => text.Take(index).Count(c => c == '\n') + 1;
}
