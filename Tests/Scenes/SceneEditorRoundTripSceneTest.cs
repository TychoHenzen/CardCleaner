using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The editor saves a scene by packing what it owns, and the overridden values on an instanced scene's children are
/// kept only when that instance is marked editable. A scene file that overrides a child of an instance without the
/// editable flag loads fine and loses those values on the first editor save (the node stays, because the instanced
/// scene still holds it). Packing every scene the way the editor does, from an instance made with the editor's edit
/// state, and comparing the stored values exposes it without opening the editor. Inline sub-resources (meshes,
/// materials, shapes) are compared by their stored values, not by their path; the comparison is in
/// <see cref="SceneRoundTripComparer"/>.
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
        var lost = SceneRoundTripComparer.LostValues(original, saved);
        original.Free();
        saved.Free();
        return lost;
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ComparingTreesThatDifferOnlyInsideAnInlineSubResourceReportsThatPath()
    {
        var original = BuildPlaceholderTree(Colors.Red);
        var saved = BuildPlaceholderTree(Colors.Blue);
        try
        {
            AssertThat(string.Join(", ", SceneRoundTripComparer.LostValues(original, saved))).IsEqual("Placeholder.mesh.material.albedo_color");
        }
        finally
        {
            original.Free();
            saved.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ComparingTreesWithEqualButSeparateInlineSubResourcesReportsNothing()
    {
        var original = BuildPlaceholderTree(Colors.Red);
        var saved = BuildPlaceholderTree(Colors.Red);
        try
        {
            AssertThat(string.Join(", ", SceneRoundTripComparer.LostValues(original, saved))).IsEmpty();
        }
        finally
        {
            original.Free();
            saved.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void InlineResourcesInAnArrayAreComparedByContent()
    {
        var original = new Node3D();
        var saved = new Node3D();
        original.SetMeta("items", new Godot.Collections.Array { new StandardMaterial3D { AlbedoColor = Colors.Red }, new StandardMaterial3D { AlbedoColor = Colors.Red } });
        saved.SetMeta("items", new Godot.Collections.Array { new StandardMaterial3D { AlbedoColor = Colors.Red }, new StandardMaterial3D { AlbedoColor = Colors.Blue } });
        try
        {
            AssertThat(string.Join(", ", SceneRoundTripComparer.LostValues(original, saved))).IsEqual("metadata/items[1].albedo_color");
        }
        finally
        {
            original.Free();
            saved.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void InlineResourcesInADictionaryAreComparedByContent()
    {
        var original = new Node3D();
        var saved = new Node3D();
        original.SetMeta("lookup", new Godot.Collections.Dictionary { { "material", new StandardMaterial3D { AlbedoColor = Colors.Red } } });
        saved.SetMeta("lookup", new Godot.Collections.Dictionary { { "material", new StandardMaterial3D { AlbedoColor = Colors.Blue } } });
        try
        {
            AssertThat(string.Join(", ", SceneRoundTripComparer.LostValues(original, saved))).IsEqual("metadata/lookup[\"material\"].albedo_color");
        }
        finally
        {
            original.Free();
            saved.Free();
        }
    }

    private static Node3D BuildPlaceholderTree(Color albedo)
    {
        var root = new Node3D { Name = "Fixture" };
        var placeholder = new MeshInstance3D
        {
            Name = "Placeholder",
            Mesh = new BoxMesh { Material = new StandardMaterial3D { AlbedoColor = albedo } },
        };
        root.AddChild(placeholder);
        placeholder.Owner = root;
        return root;
    }

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
