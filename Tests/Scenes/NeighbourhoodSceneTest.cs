using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The street neighbourhood around the shop is visual only. It is built from pack-mesh slots, one kerb box
/// primitive and a procedural sky with one sun. Scenery carries no collision, and no visible mesh enters the
/// shop and street footprint or the workshop footprint, so the walk and marker tests keep their results.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NeighbourhoodSceneTest
{
    private const string NeighbourhoodPath = "Neighbourhood";
    private const string PackPrefix = "res://Assets/Synty/PolygonMiniCity/";
    private const float PropClearanceX = 1f;
    private static readonly Rect2 ShopAndStreetFootprint = new(-16f, -8f, 40f, 24f);
    private static readonly Rect2 WorkshopFootprint = new(40f, -24f, 40f, 40f);
    private static readonly string[] CustomerMarkerNames = ["CustomerSpawn", "CustomerDespawn"];

    private Node3D _shop = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var packed = GD.Load<PackedScene>(ShopSceneProbe.ScenePath);
        _shop = packed.Instantiate<Node3D>();
        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NeighbourhoodIsInstancedOnce()
    {
        var neighbourhood = _shop.GetNodeOrNull<Node3D>(NeighbourhoodPath);
        AssertThat(neighbourhood).OverrideFailureMessage($"{NeighbourhoodPath} is missing under the shop root").IsNotNull();
        AssertThat(neighbourhood!.SceneFilePath).IsEqual("res://Scenes/Shop/Neighbourhood.tscn");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ContainsRoadKerbSidewalkFacadesNeighboursAndProps()
    {
        var neighbourhood = RequireNode(_shop, NeighbourhoodPath);
        foreach (var name in new[] { "Road", "Kerb", "FarSidewalk", "NeighbourLeft", "NeighbourRight" })
            RequireNode(neighbourhood, name);

        var facades = RequireNode(neighbourhood, "Facades");
        AssertThat(facades.GetChildCount())
            .OverrideFailureMessage($"{facades.GetPath()} holds fewer than 3 facades")
            .IsGreaterEqual(3);

        var props = RequireNode(neighbourhood, "Props");
        AssertThat(props.GetChildCount())
            .OverrideFailureMessage($"{props.GetPath()} holds fewer than 4 props")
            .IsGreaterEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ArtComesFromTheCityPack()
    {
        var neighbourhood = RequireNode(_shop, NeighbourhoodPath);
        var slots = Descendants(neighbourhood).OfType<ShopArtSlot>().ToList();
        AssertThat(slots.Count).OverrideFailureMessage("no ShopArtSlot under the neighbourhood").IsGreaterEqual(1);

        foreach (var slot in slots)
        {
            var path = slot.ArtPath;
            AssertThat(path.StartsWith(PackPrefix))
                .OverrideFailureMessage($"{slot.GetPath()} has ArtPath '{path}' outside {PackPrefix}")
                .IsTrue();
            AssertThat(path.EndsWith(".fbx"))
                .OverrideFailureMessage($"{slot.GetPath()} has ArtPath '{path}' that is not an .fbx")
                .IsTrue();
        }

        var kerb = RequireNode(neighbourhood, "Kerb");
        AssertThat(kerb is ShopArtSlot)
            .OverrideFailureMessage($"{kerb.GetPath()} is the kerb primitive and must not be a ShopArtSlot")
            .IsFalse();
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

        var suns = Descendants(_shop).OfType<DirectionalLight3D>().ToList();
        AssertThat(suns.Count).OverrideFailureMessage("the shop tree must hold exactly one DirectionalLight3D").IsEqual(1);
        AssertThat(suns[0].ShadowEnabled)
            .OverrideFailureMessage($"{suns[0].GetPath()} does not cast shadows")
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ScenerySolidnessAndFootprint()
    {
        var neighbourhood = RequireNode(_shop, NeighbourhoodPath);
        var collisions = Descendants(neighbourhood).OfType<CollisionObject3D>().ToList();
        AssertThat(collisions.Count)
            .OverrideFailureMessage($"scenery has collision: {string.Join(", ", collisions.Select(c => c.GetPath()))}")
            .IsEqual(0);

        foreach (var visual in VisibleVisuals(neighbourhood))
        {
            var footprint = Footprint(visual);
            AssertThat(footprint.Intersects(ShopAndStreetFootprint))
                .OverrideFailureMessage($"{visual.GetPath()} at {footprint} intrudes the shop and street footprint")
                .IsFalse();
            AssertThat(footprint.Intersects(WorkshopFootprint))
                .OverrideFailureMessage($"{visual.GetPath()} at {footprint} intrudes the workshop footprint")
                .IsFalse();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PropsKeepClearOfCustomerMarkers()
    {
        var props = RequireNode(RequireNode(_shop, NeighbourhoodPath), "Props");
        var propVisuals = VisibleVisuals(props).ToList();

        foreach (var markerName in CustomerMarkerNames)
        {
            var marker = RequireNode(_shop, $"World/Markers/{markerName}");
            var markerX = ((Node3D)marker).GlobalPosition.X;
            foreach (var visual in propVisuals)
            {
                var footprint = Footprint(visual);
                var gap = markerX < footprint.Position.X
                    ? footprint.Position.X - markerX
                    : markerX > footprint.End.X ? markerX - footprint.End.X : 0f;
                AssertThat(gap >= PropClearanceX)
                    .OverrideFailureMessage($"{visual.GetPath()} is {gap} m from {marker.GetPath()} in x, under {PropClearanceX} m")
                    .IsTrue();
            }
        }
    }

    private static Node3D RequireNode(Node root, string path)
    {
        var node = root.GetNodeOrNull<Node3D>(path);
        AssertThat(node).OverrideFailureMessage($"{path} is missing under {root.GetPath()}").IsNotNull();
        return node!;
    }

    private static System.Collections.Generic.IEnumerable<Node> Descendants(Node root) =>
        root.FindChildren("*", string.Empty, true, false);

    // Runtime-loaded pack art has no owner, so owned must be false to see it. Hidden placeholders are skipped.
    private static System.Collections.Generic.IEnumerable<VisualInstance3D> VisibleVisuals(Node root) =>
        Descendants(root).OfType<VisualInstance3D>().Where(visual => visual.IsVisibleInTree());

    private static Rect2 Footprint(VisualInstance3D visual)
    {
        var box = visual.GlobalTransform * visual.GetAabb();
        return new Rect2(box.Position.X, box.Position.Z, box.Size.X, box.Size.Z);
    }
}
