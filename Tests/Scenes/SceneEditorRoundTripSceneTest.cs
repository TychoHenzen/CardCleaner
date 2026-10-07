using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The editor saves a scene by packing what it owns, and the overridden values on an instanced scene's children are
/// kept only when that instance is marked editable. A scene file that overrides a child of an instance without the
/// editable flag loads fine and loses those values on the first editor save (the node stays, because the instanced
/// scene still holds it). Packing every scene the way the editor does, from an instance made with the editor's edit
/// state, and comparing every stored value exposes it without opening the editor.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SceneEditorRoundTripSceneTest
{
    private const string ScenesRoot = "res://Scenes";

    [TestCase]
    [TestCategory("Unit")]
    public static void PackingAnySceneTheWayTheEditorDoesKeepsEveryStoredValue()
    {
        var scenes = ScenePaths(ScenesRoot);
        AssertThat(scenes.Count).IsGreater(0);

        var lost = new List<string>();
        foreach (var scenePath in scenes)
            lost.AddRange(LostValues(scenePath).Select(value => $"{scenePath}: {value}"));

        AssertThat(string.Join(", ", lost)).IsEmpty();
    }

    // Instantiated with the editor's edit state (which restores the editable-instance flags), packed, and
    // instantiated again: any stored value of the first that the second lacks would be deleted from the file by a
    // save. Nodes are matched by their path from the root.
    private static List<string> LostValues(string scenePath)
    {
        var original = GD.Load<PackedScene>(scenePath).Instantiate<Node>(PackedScene.GenEditState.Main);
        var packed = new PackedScene();
        var error = packed.Pack(original);
        if (error != Error.Ok)
        {
            original.Free();
            return [$"Pack failed with {error}"];
        }

        var saved = packed.Instantiate<Node>();
        var lost = new List<string>();
        Compare(original, original, saved, lost);
        original.Free();
        saved.Free();
        return lost;
    }

    private static void Compare(Node originalRoot, Node original, Node savedRoot, List<string> lost)
    {
        var path = originalRoot.GetPathTo(original);
        var saved = savedRoot.GetNodeOrNull(path);
        if (saved == null)
        {
            lost.Add($"{path} (node)");
            return;
        }

        foreach (var property in original.GetPropertyList())
        {
            if (((PropertyUsageFlags)(long)property["usage"] & PropertyUsageFlags.Storage) == 0)
                continue;

            var name = (string)property["name"];
            if (Describe(originalRoot, original.Get(name)) != Describe(savedRoot, saved.Get(name)))
                lost.Add($"{path}.{name}");
        }

        foreach (var child in original.GetChildren())
            Compare(originalRoot, child, savedRoot, lost);
    }

    // A value as the scene file would state it: nodes by their path from the root, resources by their file.
    private static string Describe(Node root, Variant value) => value.Obj switch
    {
        Node node => root.GetPathTo(node).ToString(),
        Resource resource => resource.ResourcePath,
        _ => GD.VarToStr(value)
    };

    private static List<string> ScenePaths(string folder)
    {
        var paths = new List<string>();
        using var directory = DirAccess.Open(folder);
        if (directory == null)
            return paths;

        foreach (var sub in directory.GetDirectories())
            paths.AddRange(ScenePaths($"{folder}/{sub}"));

        paths.AddRange(directory.GetFiles().Where(file => file.EndsWith(".tscn")).Select(file => $"{folder}/{file}"));
        return paths;
    }
}
