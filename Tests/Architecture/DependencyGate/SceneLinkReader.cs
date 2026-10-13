using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// Reads the NodePath links of scene and resource text (#240 requirement 2): a NodePath property of a scripted node
/// that resolves to another scripted node is a reference the code relies on but Roslyn cannot see. A node's class is
/// its own script, or for an instanced scene the script on that scene's root.
/// </summary>
internal static partial class SceneLinkReader
{
    private const string ResPrefix = "res://";

    /// <summary>One scene link: the scripted node's class, the class of the node its NodePath names, and how often.</summary>
    internal sealed record Link(string From, string To, int Count);

    private sealed class SceneNode
    {
        public required string FullPath { get; init; }
        public string? ScriptId { get; set; }
        public string? InstanceId { get; init; }
        public List<string> Values { get; } = [];
        public string? Class { get; set; }
    }

    private sealed record ParsedFile(Dictionary<string, (string Type, string Path)> External, List<SceneNode> Nodes);

    /// <param name="files">Scene and resource files, paths relative to the project root (Scenes/X.tscn).</param>
    /// <param name="classesByFile">The class ids declared in each script file (Scripts/X.cs).</param>
    public static IReadOnlyList<Link> Read(IReadOnlyList<SourceFile> files,
        IReadOnlyDictionary<string, IReadOnlyList<string>> classesByFile)
    {
        var parsed = files.OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .Select(f => (f.RelativePath, File: Parse(f.Text))).ToList();
        var rootClassOfScene = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (path, file) in parsed)
        {
            if (!path.EndsWith(".tscn", StringComparison.Ordinal) || file.Nodes.Count == 0) continue;
            var root = file.Nodes[0];
            rootClassOfScene[ResPrefix + path] = root.ScriptId != null && file.External.TryGetValue(root.ScriptId,
                out var script) ? ScriptClass(script.Path, classesByFile) : null;
        }

        var counts = new SortedDictionary<EdgeKey, int>();
        foreach (var (_, file) in parsed) CollectLinks(file, classesByFile, rootClassOfScene, counts);
        return counts.Select(p => new Link(p.Key.From, p.Key.To, p.Value)).ToList();
    }

    private static void CollectLinks(ParsedFile file, IReadOnlyDictionary<string, IReadOnlyList<string>> classesByFile,
        Dictionary<string, string?> rootClassOfScene, SortedDictionary<EdgeKey, int> counts)
    {
        string? ClassOf(string? externalId)
        {
            if (externalId == null || !file.External.TryGetValue(externalId, out var resource)) return null;
            return resource.Type switch
            {
                "Script" => ScriptClass(resource.Path, classesByFile),
                "PackedScene" => rootClassOfScene.GetValueOrDefault(resource.Path),
                _ => null,
            };
        }

        var classByPath = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var node in file.Nodes)
        {
            node.Class = ClassOf(node.ScriptId) ?? ClassOf(node.InstanceId);
            classByPath[node.FullPath] = node.Class;
        }

        foreach (var node in file.Nodes.Where(n => n.Class != null))
        {
            foreach (Match match in node.Values.SelectMany(v => NodePathPattern().Matches(v)))
            {
                var target = classByPath.GetValueOrDefault(Resolve(node.FullPath, match.Groups[1].Value));
                if (target == null || target == node.Class) continue;
                var key = new EdgeKey(node.Class!, target);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }
    }

    // A NodePath on a node is relative to that node: ".." climbs to the parent.
    private static string Resolve(string basePath, string target)
    {
        var stack = new List<string>(basePath == "." ? [] : basePath.Split('/'));
        foreach (var part in target.Split('/'))
        {
            if (part == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            }
            else if (part is not ("" or "."))
            {
                stack.Add(part);
            }
        }

        return stack.Count == 0 ? "." : string.Join('/', stack);
    }

    // The class a script file declares: the one named after the file, else its only class.
    private static string? ScriptClass(string resPath, IReadOnlyDictionary<string, IReadOnlyList<string>> classesByFile)
    {
        var relative = resPath.StartsWith(ResPrefix, StringComparison.Ordinal) ? resPath[ResPrefix.Length..] : resPath;
        if (!classesByFile.TryGetValue(relative, out var ids)) return null;
        var stem = Path.GetFileNameWithoutExtension(relative);
        var named = ids.FirstOrDefault(id => id == stem || id.StartsWith(stem + "@", StringComparison.Ordinal));
        return named ?? (ids.Count == 1 ? ids[0] : null);
    }

    private static ParsedFile Parse(string text)
    {
        var external = new Dictionary<string, (string Type, string Path)>(StringComparer.Ordinal);
        var nodes = new List<SceneNode>();
        SceneNode? current = null;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("[ext_resource ", StringComparison.Ordinal))
            {
                var attributes = Attributes(line);
                if (attributes.TryGetValue("id", out var id))
                    external[id] = (attributes.GetValueOrDefault("type", ""), attributes.GetValueOrDefault("path", ""));
                current = null;
            }
            else if (line.StartsWith("[node ", StringComparison.Ordinal))
            {
                current = NewNode(line);
                nodes.Add(current);
            }
            else if (line.StartsWith('['))
            {
                current = null;
            }
            else if (current != null && line.Contains(" = ", StringComparison.Ordinal))
            {
                var split = line.IndexOf(" = ", StringComparison.Ordinal);
                var value = line[(split + 3)..];
                if (line[..split] == "script") current.ScriptId = ExternalId(value);
                else current.Values.Add(value);
            }
        }

        return new ParsedFile(external, nodes);
    }

    private static SceneNode NewNode(string header)
    {
        var attributes = Attributes(header);
        var name = attributes.GetValueOrDefault("name", "");
        var fullPath = attributes.TryGetValue("parent", out var parent)
            ? parent == "." ? name : $"{parent}/{name}"
            : ".";
        return new SceneNode { FullPath = fullPath, InstanceId = ExternalId(header) };
    }

    private static string? ExternalId(string text)
    {
        var match = ExternalResourcePattern().Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static Dictionary<string, string> Attributes(string header)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in AttributePattern().Matches(header)) attributes[match.Groups[1].Value] = match.Groups[2].Value;
        return attributes;
    }

    [GeneratedRegex("(\\w+)=\"([^\"]*)\"")]
    private static partial Regex AttributePattern();

    [GeneratedRegex("ExtResource\\(\"([^\"]+)\"\\)")]
    private static partial Regex ExternalResourcePattern();

    [GeneratedRegex("NodePath\\(\"([^\"]*)\"\\)")]
    private static partial Regex NodePathPattern();
}
