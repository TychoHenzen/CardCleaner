using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using FileAccess = Godot.FileAccess;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Compares a scene's canonical save (the loaded file saved again) with an editor-style save (instantiated with the
/// editor's edit state, packed and saved) as text, and names every node stanza or editable line the first has and the
/// second lacks. Nodes are keyed by the parent= path and name, so resource ids, unique ids, the header, stanza order
/// and property order do not count, and a parent_id_path attribute is ignored. A node stanza after the root without
/// parent= is reported as unparsable. Property values are not compared.
/// </summary>
public static partial class SceneStanzaCheck
{
    private const string TempFilePrefix = "scene-round-trip-";

    /// <summary>
    /// What the editor-style save <paramref name="editorPacked" /> lost from <paramref name="file" />.
    /// </summary>
    public static List<string> Missing(PackedScene file, PackedScene editorPacked) =>
        Missing(
            SavedText(file, file.ResourcePath),
            SavedText(editorPacked, $"the editor-style pack of {file.ResourcePath}"));

    /// <summary>
    /// The keys of the scene's canonical save, as <see cref="Missing(string, string)" /> compares them.
    /// </summary>
    public static IReadOnlySet<string> CanonicalKeys(string scenePath) =>
        Parse(SavedText(GD.Load<PackedScene>(scenePath), scenePath)).Keys;

    /// <summary>
    /// The unparsable stanzas of either text, then every node or editable key of <paramref name="canonicalText" />
    /// that <paramref name="savedText" /> lacks, as "missing &lt;key&gt;".
    /// </summary>
    public static List<string> Missing(string canonicalText, string savedText)
    {
        var canonical = Parse(canonicalText);
        var saved = Parse(savedText);
        var problems = canonical.Problems.Concat(saved.Problems).Distinct().ToList();
        var missing = canonical.Keys.Where(key => !saved.Keys.Contains(key)).Order(StringComparer.Ordinal);
        problems.AddRange(missing.Select(key => $"missing {key}"));
        return problems;
    }

    // The keys are "node <parent>/<name>" ("node <name>" for the root) and "editable <path>".
    private static ParsedScene Parse(string text)
    {
        var keys = new HashSet<string>();
        var problems = new List<string>();
        var nodes = 0;
        foreach (var line in text.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            if (line.StartsWith("[editable ", StringComparison.Ordinal))
            {
                keys.Add($"editable {Attribute(line, "path")}");
                continue;
            }

            if (!line.StartsWith("[node ", StringComparison.Ordinal))
                continue;

            var name = Attribute(line, "name");
            var parent = Attribute(line, "parent");
            if (name == null || (parent == null && nodes > 0))
                problems.Add($"unparsable node stanza {line}");
            else
                keys.Add(parent == null ? $"node {name}" : $"node {parent}/{name}");

            nodes++;
        }

        if (nodes == 0)
            problems.Add("no node stanza found");

        return new ParsedScene(keys, problems);
    }

    private sealed record ParsedScene(HashSet<string> Keys, List<string> Problems);

    private static string? Attribute(string header, string key) =>
        HeaderAttributes().Matches(header).FirstOrDefault(match => match.Groups[1].Value == key)?.Groups[2].Value;

    [GeneratedRegex("""(?<=[\[\s])(name|parent|path)="((?:[^"\\]|\\.)*)"(?=[\s\]])""")]
    private static partial Regex HeaderAttributes();

    // Saved to a temporary user:// file and read back; the file is deleted when the save returns or throws.
    // The name identifies the scene in the failure message, because an in-memory pack has no resource path.
    private static string SavedText(PackedScene scene, string name)
    {
        var path = $"user://{TempFilePrefix}{Guid.NewGuid():N}.tscn";
        try
        {
            var error = ResourceSaver.Save(scene, path);
            if (error != Error.Ok)
                throw new InvalidOperationException($"Saving {name} to {path} failed with {error}");

            return FileAccess.GetFileAsString(path);
        }
        finally
        {
            File.Delete(ProjectSettings.GlobalizePath(path));
        }
    }
}
