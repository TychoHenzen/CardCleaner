using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Lists the stored values of an original scene tree that its saved copy does not hold. Nodes are matched by their
/// path from their root. An inline resource (one with no file of its own) is compared by content, and an external
/// resource by its file path. Arrays and dictionaries are compared entry by entry, and each entry is named in the path.
/// </summary>
internal sealed class SceneRoundTripComparer
{
    // ASSUMPTION: 16 nested levels cover every stored value in Scenes/. Deeper nesting is reported, not compared.
    private const int MaxDepth = 16;

    private readonly Node _originalRoot;
    private readonly Node _savedRoot;
    private readonly List<string> _lost = new();
    private readonly HashSet<(ulong, ulong)> _visited = new();

    private SceneRoundTripComparer(Node originalRoot, Node savedRoot)
    {
        _originalRoot = originalRoot;
        _savedRoot = savedRoot;
    }

    internal static List<string> LostValues(Node originalRoot, Node savedRoot)
    {
        var comparer = new SceneRoundTripComparer(originalRoot, savedRoot);
        comparer.CompareNodes(originalRoot);
        return comparer._lost;
    }

    private void CompareNodes(Node original)
    {
        var path = _originalRoot.GetPathTo(original).ToString();
        var saved = _savedRoot.GetNodeOrNull(path);
        if (saved == null)
        {
            _lost.Add($"{path} (node)");
            return;
        }

        foreach (var property in StoredProperties(original))
        {
            var name = (string)property["name"];
            CompareValue(original.Get(name), saved.Get(name), Member(path, name), 0);
        }

        foreach (var child in original.GetChildren())
            CompareNodes(child);
    }

    // One stored value. The type is checked before the object is read, then the value is compared by its kind.
    private void CompareValue(Variant original, Variant saved, string path, int depth)
    {
        if (original.VariantType != saved.VariantType)
        {
            _lost.Add(path);
            return;
        }

        switch (original.VariantType)
        {
            case Variant.Type.Object:
                CompareObjects(original.Obj, saved.Obj, path, depth);
                break;
            case Variant.Type.Array:
                CompareArrays(original.AsGodotArray(), saved.AsGodotArray(), path, depth);
                break;
            case Variant.Type.Dictionary:
                CompareDictionaries(original.AsGodotDictionary(), saved.AsGodotDictionary(), path, depth);
                break;
            default:
                if (GD.VarToStr(original) != GD.VarToStr(saved))
                    _lost.Add(path);
                break;
        }
    }

    private void CompareObjects(object? original, object? saved, string path, int depth)
    {
        if (original is Resource resource)
        {
            if (saved is Resource savedResource)
                CompareResource(resource, savedResource, path, depth);
            else
                _lost.Add(path);
        }
        else if (original is Node node)
        {
            if (saved is not Node savedNode
                || _originalRoot.GetPathTo(node).ToString() != _savedRoot.GetPathTo(savedNode).ToString())
                _lost.Add(path);
        }
        else if (!ReferenceEquals(original, saved))
        {
            // A scene file holds only resources and node references, so any other object is reported, not guessed at.
            _lost.Add(path);
        }
    }

    private void CompareResource(Resource original, Resource saved, string path, int depth)
    {
        // ASSUMPTION: one object on both sides is a cached sub-resource of an instanced scene. A save cannot change it,
        // so it is equal without a content check.
        if (original.GetInstanceId() == saved.GetInstanceId())
            return;

        // Recorded before the content is read, so a resource that refers back to itself is compared once.
        if (!_visited.Add((original.GetInstanceId(), saved.GetInstanceId())))
            return;

        var inline = IsInline(original);
        if (inline != IsInline(saved))
        {
            _lost.Add(path);
            return;
        }

        if (!inline)
        {
            if (original.ResourcePath != saved.ResourcePath)
                _lost.Add(path);
            return;
        }

        if (original.GetClass() != saved.GetClass())
        {
            _lost.Add($"{path} (class {original.GetClass()} became {saved.GetClass()})");
            return;
        }

        if (depth >= MaxDepth)
        {
            _lost.Add($"{path} (depth limit)");
            return;
        }

        ComparePropertiesOf(original, saved, path, depth);
    }

    private void ComparePropertiesOf(Resource original, Resource saved, string path, int depth)
    {
        foreach (var property in StoredProperties(original))
        {
            var name = (string)property["name"];
            CompareValue(original.Get(name), saved.Get(name), Member(path, name), depth + 1);
        }
    }

    private void CompareArrays(Godot.Collections.Array original, Godot.Collections.Array saved, string path, int depth)
    {
        if (original.Count != saved.Count)
        {
            _lost.Add($"{path} (count {original.Count} became {saved.Count})");
            return;
        }

        if (depth >= MaxDepth)
        {
            _lost.Add($"{path} (depth limit)");
            return;
        }

        for (var index = 0; index < original.Count; index++)
            CompareValue(original[index], saved[index], $"{path}[{index}]", depth + 1);
    }

    private void CompareDictionaries(
        Godot.Collections.Dictionary original,
        Godot.Collections.Dictionary saved,
        string path,
        int depth)
    {
        if (depth >= MaxDepth)
        {
            _lost.Add($"{path} (depth limit)");
            return;
        }

        var originalEntries = DictionaryEntries(original);
        var savedEntries = DictionaryEntries(saved);
        foreach (var (key, value) in originalEntries)
        {
            if (savedEntries.TryGetValue(key, out var savedValue))
                CompareValue(value, savedValue, $"{path}[{key}]", depth + 1);
            else
                _lost.Add($"{path}[{key}] (missing)");
        }

        foreach (var key in savedEntries.Keys.Where(key => !originalEntries.ContainsKey(key)))
            _lost.Add($"{path}[{key}] (added)");
    }

    // Keys are matched by their text form, so the same key is found on both sides.
    private static Dictionary<string, Variant> DictionaryEntries(Godot.Collections.Dictionary dictionary)
    {
        var entries = new Dictionary<string, Variant>();
        foreach (Variant key in dictionary.Keys)
            entries[GD.VarToStr(key)] = dictionary[key];
        return entries;
    }

    // ASSUMPTION: Godot gives an inline sub-resource either no path or a "file::id" path, and no other resource
    // has either.
    private static bool IsInline(Resource resource) =>
        string.IsNullOrEmpty(resource.ResourcePath) || resource.ResourcePath.Contains("::");

    // The root's own properties have no node prefix, so they are named by the property alone.
    private static string Member(string path, string name) => path == "." ? name : $"{path}.{name}";

    private static IEnumerable<Godot.Collections.Dictionary> StoredProperties(GodotObject owner) =>
        owner.GetPropertyList()
            .Where(property => ((PropertyUsageFlags)(long)property["usage"] & PropertyUsageFlags.Storage) != 0);
}
