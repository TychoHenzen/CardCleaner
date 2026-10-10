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
/// second lacks. Nodes are keyed by parent path and name, so resource ids, unique ids, parent_id_path, the header,
/// stanza order and property order do not count. Property values are not compared.
/// </summary>
public static partial class SceneStanzaCheck
{
    public const string TempFilePrefix = "scene-round-trip-";

    /// <summary>Loads, packs and saves the scene twice and returns what the editor-style save lost.</summary>
    public static List<string> MissingAfterEditorSave(string scenePath)
    {
        var file = GD.Load<PackedScene>(scenePath);
        var instance = file.Instantiate<Node>(PackedScene.GenEditState.Main);
        try
        {
            var packed = new PackedScene();
            var error = packed.Pack(instance);
            return error == Error.Ok ? Missing(file, packed) : [$"Pack failed with {error}"];
        }
        finally
        {
            instance.Free();
        }
    }

    /// <summary>
    /// What the editor-style save <paramref name="editorPacked" /> lost from <paramref name="file" />.
    /// </summary>
    public static List<string> Missing(PackedScene file, PackedScene editorPacked) =>
        Missing(SavedText(file), SavedText(editorPacked));

    /// <summary>
    /// The keys of the scene's canonical save, as <see cref="Missing(string, string)" /> compares them.
    /// </summary>
    public static IReadOnlySet<string> CanonicalKeys(string scenePath) =>
        Parse(SavedText(GD.Load<PackedScene>(scenePath))).Keys;

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

    // Saved to a temporary user:// file and read back; the file is deleted again whatever happens.
    private static string SavedText(PackedScene scene)
    {
        var path = $"user://{TempFilePrefix}{Guid.NewGuid():N}.tscn";
        try
        {
            var error = ResourceSaver.Save(scene, path);
            if (error != Error.Ok)
                throw new InvalidOperationException($"Saving {scene.ResourcePath} to {path} failed with {error}");

            return FileAccess.GetFileAsString(path);
        }
        finally
        {
            File.Delete(ProjectSettings.GlobalizePath(path));
        }
    }
}
