using System.Collections.Generic;
using System.Linq;
using Godot;
using CardCleaner.Scripts.Features.Shop.Building;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop scene's pack-mesh slots point at the licensed pack files, keep their graybox placeholder
/// when a file is missing, and store no runtime state. The pack files live in the private Assets
/// submodule and may be absent, so nothing here requires them to exist.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopSceneArtSlotTest
{
    private const string PackFolder = "res://Assets/Synty/";

    private PackedScene _packed = null!;
    private Node3D _shop = null!;

    [BeforeTest]
    public void Setup()
    {
        _packed = GD.Load<PackedScene>(ShopSceneProbe.ScenePath);
        _shop = _packed.Instantiate<Node3D>();
        AddNode(_shop);
    }

    [TestCase]
    public void ArtSlotsPointAtPackMeshesForWallsFloorDoorsAndCounter()
    {
        var slots = new List<ShopArtSlot>();
        CollectArtSlots(_shop, slots);

        foreach (var kind in new[] { "SI_Env_Wall_0", "SI_Env_Floor", "SI_Env_Wall_Door", "SI_Prop_CheckoutCounter" })
        {
            AssertBool(slots.Exists(s => s.ArtPath.Contains(kind))).IsTrue();
        }

        foreach (var slot in slots)
        {
            AssertBool(PackMaterialCatalog.PackFolders.Any(folder => slot.ArtPath.StartsWith(folder)))
                .OverrideFailureMessage($"{slot.Name} shows {slot.ArtPath}, in no known pack folder").IsTrue();
            AssertThat(slot.Placeholder).IsNotNull();
        }
    }

    [TestCase]
    public static void ArtSlotKeepsPlaceholderWhenPackFileIsMissing()
    {
        var placeholder = new Node3D();
        var slot = new ShopArtSlot { ArtPath = PackFolder + "DoesNotExist/Missing.fbx", Placeholder = placeholder };
        slot.AddChild(placeholder);

        AddNode(slot);

        AssertBool(slot.ArtLoaded).IsFalse();
        AssertBool(placeholder.Visible).IsTrue();
    }

    [TestCase]
    public void SceneStillBuildsWhenPackFilesAreMissing()
    {
        var slots = new List<ShopArtSlot>();
        CollectArtSlots(_shop, slots);

        foreach (var slot in slots)
        {
            AssertBool(slot.ArtLoaded || slot.Placeholder!.Visible).IsTrue();
        }
    }

    [TestCase]
    public void SceneDoesNotSerializeRuntimeOnlyFields()
    {
        var state = _packed.GetState();

        for (int node = 0; node < state.GetNodeCount(); node++)
        {
            for (int i = 0; i < state.GetNodePropertyCount(node); i++)
            {
                var property = state.GetNodePropertyName(node, i).ToString();
                AssertBool(property.StartsWith('_')).IsFalse();
                AssertThat(property).IsNotEqual(nameof(ShopArtSlot.ArtLoaded));
            }
        }
    }

    private static void CollectArtSlots(Node node, List<ShopArtSlot> slots)
    {
        if (node is ShopArtSlot slot)
            slots.Add(slot);

        foreach (var child in node.GetChildren())
            CollectArtSlots(child, slots);
    }
}
