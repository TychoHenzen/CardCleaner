using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>Reads the gate's inputs from a checkout: script and addon sources, and scene and resource text.</summary>
internal static class RepositorySources
{
    // Tool folders that sit under Scripts/ on a dev machine but are not part of the game.
    private static readonly string[] SkippedScriptFolders = ["Scripts/.serena/", "Scripts/.claude/"];

    /// <summary>Every Scripts/**/*.cs file.</summary>
    public static IReadOnlyList<SourceFile> Scripts(string projectRoot) =>
        Read(projectRoot, "Scripts", "*.cs",
            path => !SkippedScriptFolders.Any(f => path.StartsWith(f, StringComparison.Ordinal)));

    /// <summary>Every addons/**/*.cs file except gdUnit4's: compiled alongside, never counted as nodes.</summary>
    public static IReadOnlyList<SourceFile> Addons(string projectRoot) =>
        Read(projectRoot, "addons", "*.cs", path => !path.StartsWith("addons/gdUnit4/", StringComparison.Ordinal));

    /// <summary>
    /// Every .tscn file under Scenes/ and Data/. A .tres resource has no [node] stanzas, so its NodePath values name
    /// no node of a scene and it adds no link.
    /// </summary>
    public static IReadOnlyList<SourceFile> Scenes(string projectRoot) =>
        new[] { "Scenes", "Data" }
            .SelectMany(folder => Read(projectRoot, folder, "*.tscn", _ => true))
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToList();

    private static List<SourceFile> Read(string projectRoot, string folder, string pattern, Func<string, bool> keep)
    {
        var directory = Path.Combine(projectRoot, folder);
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Select(file => (File: file, Relative: Path.GetRelativePath(projectRoot, file).Replace('\\', '/')))
            .Where(p => keep(p.Relative))
            .OrderBy(p => p.Relative, StringComparer.Ordinal)
            .Select(p => new SourceFile(p.Relative, File.ReadAllText(p.File)))
            .ToList();
    }
}
