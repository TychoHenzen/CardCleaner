using System.Collections.Generic;
using System.IO;
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
    public static void NoSceneStoresAHiddenPlaceholder()
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

    private static ShopArtSlot AddSlot(string artPath, out MeshInstance3D placeholder)
    {
        placeholder = new MeshInstance3D();
        var slot = new ShopArtSlot { ArtPath = artPath, Placeholder = placeholder };
        slot.AddChild(placeholder);
        return AddNode(slot);
    }

    private static List<string> ScenesWithArtSlots(string folder)
    {
        var found = new List<string>();
        using var directory = DirAccess.Open(folder);
        if (directory == null)
            return found;

        foreach (var sub in directory.GetDirectories())
            found.AddRange(ScenesWithArtSlots($"{folder}/{sub}"));

        foreach (var file in directory.GetFiles())
        {
            var path = $"{folder}/{file}";
            if (file.EndsWith(".tscn") && File.ReadAllText(ProjectSettings.GlobalizePath(path)).Contains(nameof(ShopArtSlot)))
                found.Add(path);
        }

        return found;
    }

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
