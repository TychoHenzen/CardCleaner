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
/// loaded again with the cache ignored, so the reloaded copy's inline sub-resources (meshes, materials, shapes) are
/// fresh objects, and a value the save dropped or changed shows up as a difference. An in-memory copy would share them
/// with the original and hide the loss. The comparer only reads both trees; only the optional test hook changes the
/// reloaded copy. The comparison itself is in <see cref="SceneRoundTripComparer"/>.
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
            var reload = SaveAndReload(packed, original, path);
            reloaded = reload.Scene;
            if (reloaded == null)
                return [reload.Failure];

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

    // The reloaded scene, or the message of the step that failed. Failure is empty when the reload succeeds.
    private sealed record ReloadResult(PackedScene? Scene, string Failure);

    // Packs the instance into the given pack, saves it to path and loads that file with the cache ignored.
    // A failed step returns no scene and its message, which the caller reports as a lost value.
    private static ReloadResult SaveAndReload(PackedScene packed, Node original, string path)
    {
        var packError = packed.Pack(original);
        if (packError != Error.Ok)
            return new ReloadResult(null, $"Pack failed with {packError}");

        var saveError = ResourceSaver.Save(packed, path);
        if (saveError != Error.Ok)
            return new ReloadResult(null, $"Save failed with {saveError}");

        var reloaded = ResourceLoader.Load<PackedScene>(path, null, ResourceLoader.CacheMode.Ignore);
        if (reloaded == null)
            return new ReloadResult(null, $"Load failed for {path}");

        return new ReloadResult(reloaded, string.Empty);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ARoundTripThatLosesAnInlineValueIsReported()
    {
        var lost = PlaceholderRoundTripLosses(saved => PlaceholderMaterial(saved).AlbedoColor = Colors.Blue);
        AssertThat(lost).IsEqual("Placeholder.mesh.material.albedo_color");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ARoundTripWithNoChangeReportsNothing()
    {
        AssertThat(PlaceholderRoundTripLosses()).IsEmpty();
    }

    // Packs a placeholder tree, round-trips it through a file and returns its losses joined, for one assertion.
    private static string PlaceholderRoundTripLosses(Action<Node>? afterReload = null)
    {
        var root = SceneRoundTripComparerTest.BuildPlaceholderTree(Colors.Red);
        var fixture = new PackedScene();
        try
        {
            AssertThat(fixture.Pack(root)).IsEqual(Error.Ok);
            return string.Join(", ", RoundTripLosses(fixture, afterReload));
        }
        finally
        {
            fixture.Dispose();
            root.Free();
        }
    }

    private static StandardMaterial3D PlaceholderMaterial(Node root) =>
        (StandardMaterial3D)((BoxMesh)root.GetNode<MeshInstance3D>("Placeholder").Mesh).Material;

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
