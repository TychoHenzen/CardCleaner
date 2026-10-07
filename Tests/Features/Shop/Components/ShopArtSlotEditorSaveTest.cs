using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
/// ShopArtSlot is a [Tool] script, so the editor shows the real art and hides the graybox placeholder. The
/// placeholder's Visible flag is an ordinary property: saved as false, it would hide the fallback box in the
/// game for good. The slot therefore restores it around every editor save, and no scene may store it hidden.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopArtSlotEditorSaveTest
{
    private const string OwnModelPath = "res://Assets/Models/Button.fbx";
    private const string MissingPath = "res://Assets/Synty/SimpleShopInterior/DoesNotExist.fbx";
    private const string ScenesRoot = "res://Scenes";

    [TestCase]
    [TestCategory("Unit")]
    public static void PreSaveRestoresThePlaceholderAndPostSaveHidesItAgain()
    {
        var slot = AddSlot(OwnModelPath, out var placeholder);
        AssertBool(slot.ArtLoaded).IsTrue();
        AssertBool(placeholder.Visible).IsFalse();

        slot.Notification((int)Node.NotificationEditorPreSave);
        AssertBool(placeholder.Visible).IsTrue();

        slot.Notification((int)Node.NotificationEditorPostSave);
        AssertBool(placeholder.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SavingKeepsThePlaceholderVisibleWhenTheArtIsMissing()
    {
        var slot = AddSlot(MissingPath, out var placeholder);
        AssertBool(slot.ArtLoaded).IsFalse();

        slot.Notification((int)Node.NotificationEditorPreSave);
        AssertBool(placeholder.Visible).IsTrue();

        slot.Notification((int)Node.NotificationEditorPostSave);
        AssertBool(placeholder.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SavingASlotWithoutAPlaceholderDoesNothing()
    {
        var slot = AddNode(new ShopArtSlot { ArtPath = OwnModelPath });

        slot.Notification((int)Node.NotificationEditorPreSave);
        slot.Notification((int)Node.NotificationEditorPostSave);

        AssertBool(slot.ArtLoaded).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoSceneFileStoresAHiddenPlaceholder()
    {
        var scenes = ScenesWithArtSlots(ScenesRoot);
        AssertThat(scenes.Count).IsGreater(0);

        var hidden = new List<string>();
        foreach (var scenePath in scenes)
        {
            // Instantiated but never added to the tree, so _Ready has not hidden anything: what is read here is
            // exactly what the scene file stores, including overrides on instanced sub-scenes.
            var root = GD.Load<PackedScene>(scenePath).Instantiate<Node>();
            foreach (var slot in Slots(root))
            {
                if (slot.Placeholder is { Visible: false })
                    hidden.Add($"{scenePath}: {root.GetPathTo(slot.Placeholder)}");
            }

            root.Free();
        }

        AssertThat(string.Join(", ", hidden)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnEditorSaveOfEverySceneWithAnArtSlotStoresNoHiddenPlaceholderAndNoArt()
    {
        var scenes = ScenesWithArtSlots(ScenesRoot);
        AssertThat(scenes.Count).IsGreater(0);

        var problems = new List<string>();
        foreach (var scenePath in scenes)
            problems.AddRange(SaveProblems(scenePath));

        AssertThat(string.Join(", ", problems)).IsEmpty();
    }

    private static ShopArtSlot AddSlot(string artPath, out MeshInstance3D placeholder)
    {
        placeholder = new MeshInstance3D();
        var slot = new ShopArtSlot { ArtPath = artPath, Placeholder = placeholder };
        slot.AddChild(placeholder);
        return AddNode(slot);
    }

    // Every scene under the folder whose instantiated graph holds a slot, so a scene that reaches a slot only
    // through an instanced sub-scene (the arcade cabinet through the cabinet assembly) is found as well.
    private static List<string> ScenesWithArtSlots(string folder)
    {
        var found = new List<string>();
        foreach (var scenePath in ScenePaths(folder))
        {
            var root = GD.Load<PackedScene>(scenePath).Instantiate<Node>();
            if (Slots(root).Any())
                found.Add(scenePath);
            root.Free();
        }

        return found;
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

    // Plays what the editor does when it saves a scene whose slots show their art: _Ready has run (art loaded,
    // placeholders hidden), PreSave is delivered to every node, the scene is packed, PostSave follows. The packed
    // copy must hold the authored nodes only (the Art child has no owner) and no hidden placeholder.
    private static List<string> SaveProblems(string scenePath)
    {
        var problems = new List<string>();
        var root = GD.Load<PackedScene>(scenePath).Instantiate<Node>();
        var authoredNodes = CountNodes(root);
        AddNode(root);
        root.PropagateNotification((int)Node.NotificationEditorPreSave);
        var packed = new PackedScene();
        var error = packed.Pack(root);
        root.PropagateNotification((int)Node.NotificationEditorPostSave);

        if (error != Error.Ok)
        {
            problems.Add($"{scenePath}: Pack failed with {error}");
        }
        else
        {
            var saved = packed.Instantiate<Node>();
            if (CountNodes(saved) != authoredNodes)
                problems.Add($"{scenePath}: saved {CountNodes(saved)} nodes, authored {authoredNodes}");
            foreach (var slot in Slots(saved).Where(slot => slot.Placeholder is { Visible: false }))
                problems.Add($"{scenePath}: {saved.GetPathTo(slot.Placeholder)} saved hidden");
            saved.Free();
        }

        root.Free();
        return problems;
    }

    private static int CountNodes(Node node) =>
        1 + node.GetChildren().Sum(CountNodes);

    private static IEnumerable<ShopArtSlot> Slots(Node node)
    {
        if (node is ShopArtSlot slot)
            yield return slot;

        foreach (var child in node.GetChildren())
        {
            foreach (var nested in Slots(child))
                yield return nested;
        }
    }
}
