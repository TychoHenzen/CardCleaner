using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The Synty packs ship their meshes without a usable texture and as single-sided slabs, so every art slot
/// in the shop applies its pack's atlas with a double-sided material: textured, and visible from both sides.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopArtMaterialSceneTest
{
    private const string PackFolder = "res://Assets/Synty/";
    private const string OwnModelPath = "res://Assets/Models/Button.fbx";
    private const string MissingPath = "res://Assets/Synty/SimpleShopInterior/DoesNotExist.fbx";

    private Node3D _shop = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();

        // The player is not needed to look at the art, so it must not move or take input meanwhile.
        ShopScenePlayer.Sideline(_shop);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryPackSlotInTheShopLoadsItsMeshAndIsTextured()
    {
        var packSlots = Slots(_shop).Where(slot => slot.ArtPath.StartsWith(PackFolder, StringComparison.Ordinal)).ToList();

        AssertThat(packSlots.Count).IsGreater(0);
        var untextured = packSlots.Where(slot => !slot.ArtLoaded || !slot.ArtTextured).Select(slot => slot.GetPath().ToString());
        AssertThat(string.Join(", ", untextured)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryMeshOfAPackSlotHasTheAtlasAndFacesBothWays()
    {
        var packSlots = Slots(_shop).Where(slot => slot.ArtPath.StartsWith(PackFolder, StringComparison.Ordinal)).ToList();

        var offenders = new List<string>();
        foreach (var slot in packSlots)
        {
            var meshes = Meshes(slot.GetNode("Art")).ToList();
            if (meshes.Count == 0)
                offenders.Add($"{slot.GetPath()} has no mesh");
            offenders.AddRange(meshes
                .Where(mesh => mesh.MaterialOverride is not StandardMaterial3D
                {
                    AlbedoTexture: not null,
                    CullMode: BaseMaterial3D.CullModeEnum.Disabled
                })
                .Select(mesh => $"{mesh.GetPath()} is not a double-sided textured material"));
        }

        AssertThat(string.Join(", ", offenders)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SlotsOfTheSamePackShareOneMaterial()
    {
        var walls = Slots(_shop).Where(slot => slot.ArtPath.EndsWith("SI_Env_Wall_01.fbx", StringComparison.Ordinal)).ToList();

        AssertThat(walls.Count).IsGreater(1);
        var materials = walls.SelectMany(slot => Meshes(slot.GetNode("Art"))).Select(mesh => mesh.MaterialOverride).Distinct();
        AssertThat(materials.Count()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASlotOutsideTheKnownPacksLoadsItsMeshUntextured()
    {
        var slot = AddNode(new ShopArtSlot { ArtPath = OwnModelPath });

        AssertBool(slot.ArtLoaded).IsTrue();
        AssertBool(slot.ArtTextured).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASlotWhoseFileIsMissingStaysUntexturedAndKeepsItsPlaceholder()
    {
        var placeholder = new MeshInstance3D();
        var slot = new ShopArtSlot { ArtPath = MissingPath, Placeholder = placeholder };
        slot.AddChild(placeholder);
        AddNode(slot);

        AssertBool(slot.ArtLoaded).IsFalse();
        AssertBool(slot.ArtTextured).IsFalse();
        AssertBool(placeholder.Visible).IsTrue();
    }

    private static IEnumerable<ShopArtSlot> Slots(Node root) =>
        Descendants(root).OfType<ShopArtSlot>();

    private static IEnumerable<MeshInstance3D> Meshes(Node root) =>
        Descendants(root).Append(root).OfType<MeshInstance3D>();

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
