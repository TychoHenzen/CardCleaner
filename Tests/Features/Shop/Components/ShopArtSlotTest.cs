using Godot;
using CardCleaner.Scripts.Features.Shop.Building;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
/// ShopArtSlot reports a pack art scene as textured only when a mesh under it received the pack's atlas.
/// The scenes are packed in memory and registered under a pack folder path for one test each, so the slot
/// loads them through its real path and no art file is needed. Both tests still need the pack's imported
/// colour atlas from the Assets submodule. The mesh test is the control that fails when that atlas is missing,
/// which keeps the mesh-less test from passing vacuously.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopArtSlotTest
{
    // Not files on disk: each path only exists in the resource cache while its test runs.
    private const string MeshlessArtPath = "res://Assets/Synty/SimpleShopInterior/MeshlessArtTest.tscn";
    private const string MeshArtPath = "res://Assets/Synty/SimpleShopInterior/MeshArtTest.tscn";

    [TestCase]
    public static void ArtSceneWithoutAnyMeshIsLoadedButNotTextured()
    {
        var scene = RegisterArtScene(MeshlessArtPath, new Node3D());
        try
        {
            var slot = AddNode(new ShopArtSlot { ArtPath = MeshlessArtPath });

            AssertBool(slot.ArtLoaded).IsTrue();
            AssertBool(slot.ArtTextured).IsFalse();
        }
        finally
        {
            UnregisterArtScene(scene);
        }
    }

    [TestCase]
    public static void ArtSceneWithAMeshIsTextured()
    {
        var scene = RegisterArtScene(MeshArtPath, new MeshInstance3D());
        try
        {
            var slot = AddNode(new ShopArtSlot { ArtPath = MeshArtPath });

            AssertBool(slot.ArtLoaded).IsTrue();
            // ASSUMPTION: the SimpleShopInterior atlas is imported, since this mesh control needs it to pass.
            AssertBool(slot.ArtTextured).IsTrue();
        }
        finally
        {
            UnregisterArtScene(scene);
        }
    }

    [TestCase]
    public static void TintIsMultipliedIntoTheAtlasPerTint()
    {
        var tint = new Color(1f, 0.9f, 0.8f);
        var scene = RegisterArtScene(MeshArtPath, new MeshInstance3D());
        try
        {
            var tinted = AddNode(new ShopArtSlot { ArtPath = MeshArtPath, Tint = tint });
            var plain = AddNode(new ShopArtSlot { ArtPath = MeshArtPath });

            var tintedMaterial = (StandardMaterial3D)tinted.GetNode<MeshInstance3D>("Art").MaterialOverride;
            var plainMaterial = (StandardMaterial3D)plain.GetNode<MeshInstance3D>("Art").MaterialOverride;
            AssertThat(tintedMaterial.AlbedoColor).IsEqual(tint);
            AssertThat(tintedMaterial.AlbedoTexture).IsNotNull();
            AssertThat(plainMaterial.AlbedoColor).IsEqual(Colors.White);
            AssertThat(tintedMaterial).IsNotSame(plainMaterial);
        }
        finally
        {
            UnregisterArtScene(scene);
        }
    }

    private static PackedScene RegisterArtScene(string path, Node3D root)
    {
        var scene = new PackedScene();
        scene.Pack(root);
        root.Free();
        scene.TakeOverPath(path);
        return scene;
    }

    private static void UnregisterArtScene(PackedScene scene) => scene.ResourcePath = string.Empty;
}
