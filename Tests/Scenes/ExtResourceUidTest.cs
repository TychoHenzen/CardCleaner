using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// A scene or resource names each external resource by uid= and path=. When the uid no longer matches the target's
/// own UID (its .uid sidecar, its .import file, or the header of a .tscn/.tres target), Godot prints
/// "ext_resource, invalid UID" and falls back to the path. These checks read the files only, so they hold on a fresh
/// clone as long as the sidecars they compare against are tracked.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class ExtResourceUidTest
{
    private static readonly string[] ScannedRoots = ["res://Scenes", "res://Data", "res://Assets"];

    // ASSUMPTION: the submodule material keeps its stale foliage UID until the Assets two-commit fix lands (#185).
    // KnownStaleReferenceIsStillStale fails once it is fixed, so these entries have to be removed then.
    private const string KnownStaleFile = "Assets/Materials/Conveyor_Straight.tres";
    private const string KnownStalePath = "res://Assets/Models/Textures/foliage(Clone).png";
    private const string KnownStaleUid = "uid://cjf73kdfrsdm2";

    [GeneratedRegex(@"^\[ext_resource\b(?<attrs>.*)\]\s*$")]
    private static partial Regex ExtResourceLine();

    [GeneratedRegex(@"(?<key>\w+)=""(?<value>[^""]*)""")]
    private static partial Regex Attribute();

    [GeneratedRegex(@"^uid=""(?<uid>[^""]+)""", RegexOptions.Multiline)]
    private static partial Regex ImportUid();

    [GeneratedRegex(@"\buid=""(?<uid>[^""]+)""")]
    private static partial Regex HeaderUid();

    /// <summary>
    /// One entry per ext_resource line of <paramref name="text" /> whose uid= differs from what
    /// <paramref name="uidOf" /> returns for its path= (null means the target has no UID source), as
    /// "file:line: path has uid, target has uid" or "file:line: path has uid, target has no UID source".
    /// </summary>
    public static IEnumerable<string> StaleReferences(string file, string text, Func<string, string?> uidOf)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = ExtResourceLine().Match(lines[i]);
            if (!match.Success)
                continue;
            var attributes = Attribute().Matches(match.Groups["attrs"].Value)
                .ToDictionary(a => a.Groups["key"].Value, a => a.Groups["value"].Value);
            if (!attributes.TryGetValue("uid", out var uid) || !attributes.TryGetValue("path", out var path))
                continue;
            var actual = uidOf(path);
            if (actual == uid)
                continue;
            var target = actual is null ? "target has no UID source" : $"target has {actual}";
            yield return $"{file}:{i + 1}: {path} has {uid}, {target}";
        }
    }

    /// <summary>
    /// The UID Godot assigns to <paramref name="resPath" />: its .uid sidecar, the uid= of its .import file, or the
    /// header uid= of a .tscn/.tres file; null when none of those exists.
    /// </summary>
    public static string? ProjectUidOf(string resPath)
    {
        var file = ProjectSettings.GlobalizePath(resPath);
        if (File.Exists(file + ".uid"))
            return File.ReadAllText(file + ".uid").Trim();
        if (File.Exists(file + ".import"))
        {
            var match = ImportUid().Match(File.ReadAllText(file + ".import"));
            return match.Success ? match.Groups["uid"].Value : null;
        }

        if ((file.EndsWith(".tscn", StringComparison.Ordinal) || file.EndsWith(".tres", StringComparison.Ordinal))
            && File.Exists(file))
        {
            var match = HeaderUid().Match(File.ReadLines(file).FirstOrDefault() ?? "");
            return match.Success ? match.Groups["uid"].Value : null;
        }

        return null;
    }

    /// <summary>Every stale reference in the .tscn and .tres files under the project folder <paramref name="root" />.
    /// </summary>
    private static List<string> StaleReferencesUnder(string root, Func<string, string?> uidOf)
    {
        var projectRoot = ProjectSettings.GlobalizePath("res://");
        return Directory.EnumerateFiles(ProjectSettings.GlobalizePath(root), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".tscn", StringComparison.Ordinal)
                || path.EndsWith(".tres", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .SelectMany(path => StaleReferences(
                Path.GetRelativePath(projectRoot, path).Replace('\\', '/'),
                File.ReadAllText(path),
                uidOf))
            .ToList();
    }

    private static List<string> ProjectStaleReferences() =>
        ScannedRoots.SelectMany(root => StaleReferencesUnder(root, ProjectUidOf)).ToList();

    private static bool IsKnownStale(string entry) =>
        entry.StartsWith(KnownStaleFile + ":", StringComparison.Ordinal)
        && entry.Contains($": {KnownStalePath} has {KnownStaleUid},", StringComparison.Ordinal);

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryExtResourceUidMatchesItsTarget()
    {
        var stale = ProjectStaleReferences().Where(entry => !IsKnownStale(entry));

        // Joined into one string so a failure names every stale reference with its file and line.
        AssertThat(string.Join(System.Environment.NewLine, stale)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void KnownStaleReferenceIsStillStale()
    {
        // When this fails the Assets fix has landed: remove the KnownStale constants, IsKnownStale and this test.
        AssertThat(ProjectStaleReferences().Count(IsKnownStale)).IsEqual(1);
    }

    [TestCase("res://Scenes")]
    [TestCase("res://Data")]
    [TestCase("res://Assets")]
    [TestCategory("Unit")]
    public static void EveryScannedRootHasUidReferences(string root)
    {
        // With every UID source missing each uid= reference is reported, so a root the scan cannot read fails here.
        AssertThat(StaleReferencesUnder(root, _ => null).Count).IsGreater(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MismatchedUidIsReportedWithFileAndLine()
    {
        const string text = "[gd_scene format=3]\n\n" +
            "[ext_resource type=\"Script\" uid=\"uid://old\" path=\"res://A.cs\" id=\"1\"]\n";

        var stale = StaleReferences("Scenes/X.tscn", text, _ => "uid://new").ToList();

        AssertThat(stale).ContainsExactly("Scenes/X.tscn:3: res://A.cs has uid://old, target has uid://new");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TargetWithoutUidSourceIsReported()
    {
        const string text = "[ext_resource type=\"Script\" path=\"res://A.cs\" uid=\"uid://old\" id=\"1\"]\r\n";

        var stale = StaleReferences("Scenes/X.tscn", text, _ => null).ToList();

        AssertThat(stale).ContainsExactly("Scenes/X.tscn:1: res://A.cs has uid://old, target has no UID source");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MatchingAndPathOnlyReferencesPass()
    {
        const string text = "[ext_resource type=\"Script\" uid=\"uid://same\" path=\"res://A.cs\" id=\"1\"]\n" +
            "[ext_resource type=\"Script\" path=\"res://B.cs\" id=\"2\"]\n" +
            "[node name=\"Root\" type=\"Node\" uid=\"uid://ignored\"]\n";

        AssertThat(StaleReferences("Scenes/X.tscn", text, _ => "uid://same").ToList()).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ScriptSidecarIsTheProjectUidSource()
    {
        var sidecar = ProjectSettings.GlobalizePath("res://Tests/TestSuiteNamingTest.cs.uid");

        AssertThat(ProjectUidOf("res://Tests/TestSuiteNamingTest.cs")).IsEqual(File.ReadAllText(sidecar).Trim());
    }
}
