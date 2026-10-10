using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>
/// Source scans shared by the seam guards: lists the .cs files under a res:// folder, reads each one with comments and
/// literals blanked, and formats a match as relative/path.cs:line text.
/// </summary>
internal static class SourceScan
{
    private const string ProjectRoot = "res://";

    // ASSUMPTION: the .cs sources are on disk when the suite runs (editor, headless run or CI checkout). An export
    // without them finds no files and fails the file-count check instead of passing.
    internal static List<string> CsFiles(string resRoot)
    {
        return Directory.EnumerateFiles(ProjectSettings.GlobalizePath(resRoot), "*.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetExtension(file) == ".cs")
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
    }

    internal static string Blanked(string osPath) =>
        TestSuiteNamingTest.BlankCommentsAndStrings(File.ReadAllText(osPath));

    internal static string RelativePath(string osPath) =>
        Path.GetRelativePath(ProjectSettings.GlobalizePath(ProjectRoot), osPath).Replace('\\', '/');

    // One hit as "relative/path.cs:line text". The line is counted in the blanked source, where newlines are kept.
    internal static string FormatHit(string relative, string blanked, Match match) =>
        FormattableString.Invariant($"{relative}:{LineOf(blanked, match.Index)} {match.Value}");

    private static int LineOf(string text, int index) => text.Take(index).Count(c => c == '\n') + 1;
}
