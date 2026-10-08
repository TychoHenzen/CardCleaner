using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The editor saves a scene by packing what it owns, and the overridden values on an instanced scene's children are
/// kept only when that instance is marked editable. A scene file that overrides a child of an instance without the
/// editable flag loads fine and loses those values on the first editor save (the node stays, because the instanced
/// scene still holds it). Packing every scene the way the editor does, from an instance made with the editor's edit
/// state, and comparing the stored values exposes it without opening the editor. The scene is saved to a file and
/// loaded again with the cache ignored, so its inline sub-resources (meshes, materials, shapes) are new objects that
/// the comparison can change; an in-memory copy shares them with the original and would hide a loss. The comparison
/// itself is in <see cref="SceneRoundTripComparer"/>.
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
            lost.AddRange(RoundTripLosses(GD.Load<PackedScene>(scenePath)).Select(value => $"{scenePath}: {value}"));

        AssertThat(string.Join(", ", lost)).IsEmpty();
    }

    // Packs the scene the way the editor does, saves the pack to a temporary file and loads that file again with the
    // cache ignored, then compares the stored values of the original instance with those of the reloaded copy. The
    // cache is ignored only for the file itself: Godot gives the copy its own inline sub-resources, and the scripts,
    // textures and instanced scenes it refers to still come from the cache. The optional hook changes only the copy,
    // which is how a test makes a loss.
    internal static List<string> RoundTripLosses(PackedScene scene, Action<Node>? afterReload = null)
    {
        var path = $"user://scene-round-trip-{Guid.NewGuid():N}.tscn";
        Node? original = null;
        PackedScene? packed = null;
        PackedScene? reloaded = null;
        Node? saved = null;
        try
        {
            original = scene.Instantiate<Node>(PackedScene.GenEditState.Main);
            packed = new PackedScene();
            var packError = packed.Pack(original);
            if (packError != Error.Ok)
                return [$"Pack failed with {packError}"];

            var saveError = ResourceSaver.Save(packed, path);
            if (saveError != Error.Ok)
                return [$"Save failed with {saveError}"];

            reloaded = ResourceLoader.Load<PackedScene>(path, null, ResourceLoader.CacheMode.Ignore);
            if (reloaded == null)
                return [$"Load failed for {path}"];

            saved = reloaded.Instantiate<Node>(PackedScene.GenEditState.Main);
            afterReload?.Invoke(saved);
            return SceneRoundTripComparer.LostValues(original, saved);
        }
        finally
        {
            original?.Free();
            saved?.Free();
            packed?.Dispose();
            reloaded?.Dispose();
            var absolutePath = ProjectSettings.GlobalizePath(path);
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
        }
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

    [TestCase]
    [TestCategory("Unit")]
    public static void ARoundTripThatLosesAnInlineValueIsReported()
    {
        var root = BuildPlaceholderTree(Colors.Red);
        var fixture = new PackedScene();
        try
        {
            AssertThat(fixture.Pack(root)).IsEqual(Error.Ok);
            var lost = RoundTripLosses(fixture, saved => ((StandardMaterial3D)((BoxMesh)saved.GetNode<MeshInstance3D>("Placeholder").Mesh).Material).AlbedoColor = Colors.Blue);
            AssertThat(string.Join(", ", lost)).IsEqual("Placeholder.mesh.material.albedo_color");
        }
        finally
        {
            fixture.Dispose();
            root.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ARoundTripWithNoChangeReportsNothing()
    {
        var root = BuildPlaceholderTree(Colors.Red);
        var fixture = new PackedScene();
        try
        {
            AssertThat(fixture.Pack(root)).IsEqual(Error.Ok);
            AssertThat(string.Join(", ", RoundTripLosses(fixture))).IsEmpty();
        }
        finally
        {
            fixture.Dispose();
            root.Free();
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
