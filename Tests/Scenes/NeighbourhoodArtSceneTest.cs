using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The neighbourhood art comes from the city pack through ShopArtSlot nodes. Where a slot has no pack file
/// the box placeholder stays, and the kerb is the one plain box primitive. A procedural sky with one sun
/// replaces the flat background.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NeighbourhoodArtSceneTest
{
    private const string PackPrefix = "res://Assets/Synty/PolygonMiniCity/";
    private const string ArtNodeName = "Art";

    private Node3D _shop = null!;
    private Node3D _neighbourhood = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        AddNode(_shop);
        await ShopPauseRig.SettleAsync();
        _neighbourhood = SceneNodes.Require(_shop, "Neighbourhood");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ArtComesFromTheCityPack()
    {
        foreach (var slot in Slots())
        {
            var path = slot.ArtPath;
            AssertThat(path.StartsWith(PackPrefix))
                .OverrideFailureMessage($"{slot.GetPath()} has ArtPath '{path}' outside {PackPrefix}")
                .IsTrue();
            AssertThat(path.EndsWith(".fbx"))
                .OverrideFailureMessage($"{slot.GetPath()} has ArtPath '{path}' that is not an .fbx")
                .IsTrue();
        }

        var kerb = SceneNodes.Require(_neighbourhood, "Kerb");
        AssertThat(kerb is ShopArtSlot)
            .OverrideFailureMessage($"{kerb.GetPath()} is the kerb primitive and must not be a ShopArtSlot")
            .IsFalse();
    }

    // Headless stand-in for the doorway screenshot (owner decision 2026-11-01: no acceptance check may need a
    // window; the look is judged by eye and never gates merging). After the shop loads, every slot must have
    // loaded its pack mesh, hidden its placeholder and carry a material on each of its meshes.
    [TestCase]
    [TestCategory("Unit")]
    public void EveryArtSlotLoadedItsMeshHidPlaceholderAndHasMaterial()
    {
        foreach (var slot in Slots())
        {
            AssertThat(slot.ArtLoaded)
                .OverrideFailureMessage($"{slot.GetPath()} did not load its Art mesh from '{slot.ArtPath}'")
                .IsTrue();
            AssertThat(slot.Placeholder != null && !slot.Placeholder.Visible)
                .OverrideFailureMessage($"{slot.GetPath()} keeps its placeholder visible")
                .IsTrue();

            var art = slot.GetNodeOrNull<Node3D>(ArtNodeName);
            AssertThat(art).OverrideFailureMessage($"{slot.GetPath()} has no {ArtNodeName} child").IsNotNull();
            var meshes = SceneNodes.Descendants(art!).Append(art!).OfType<MeshInstance3D>().ToList();
            AssertThat(meshes.Count).OverrideFailureMessage($"{art!.GetPath()} holds no mesh").IsGreaterEqual(1);
            foreach (var mesh in meshes)
            {
                AssertThat(mesh.MaterialOverride != null)
                    .OverrideFailureMessage($"{mesh.GetPath()} has no material")
                    .IsTrue();
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SkyAndSunReplaceTheFlatBackground()
    {
        var environment = _shop.GetNodeOrNull<WorldEnvironment>("Environment");
        AssertThat(environment).OverrideFailureMessage("Environment WorldEnvironment is missing").IsNotNull();

        var env = environment!.Environment;
        AssertThat(env.BackgroundMode == Environment.BGMode.Sky)
            .OverrideFailureMessage("the shop background is not the Sky mode")
            .IsTrue();
        AssertThat(env.Sky?.SkyMaterial is ProceduralSkyMaterial)
            .OverrideFailureMessage("the shop sky does not use a ProceduralSkyMaterial")
            .IsTrue();

        var suns = SceneNodes.Descendants(_shop).OfType<DirectionalLight3D>().ToList();
        AssertThat(suns.Count)
            .OverrideFailureMessage("the shop tree must hold exactly one DirectionalLight3D")
            .IsEqual(1);
        AssertThat(suns[0].ShadowEnabled)
            .OverrideFailureMessage($"{suns[0].GetPath()} does not cast shadows")
            .IsTrue();
    }

    private System.Collections.Generic.List<ShopArtSlot> Slots()
    {
        var slots = SceneNodes.Descendants(_neighbourhood).OfType<ShopArtSlot>().ToList();
        AssertThat(slots.Count).OverrideFailureMessage("no ShopArtSlot under the neighbourhood").IsGreaterEqual(1);
        return slots;
    }
}
