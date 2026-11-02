using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The neighbourhood is visual only: it carries no collision, and no visible mesh enters the shop and street
/// footprint or the workshop footprint, so the walk and marker tests keep their results. Props also keep a
/// clear gap in x from the customer markers.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NeighbourhoodScenerySceneTest
{
    private const float PropClearanceX = 1f;
    private static readonly string[] CustomerMarkerNames = ["CustomerSpawn", "CustomerDespawn"];

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
    public void ScenerySolidnessAndFootprint()
    {
        var collisions = SceneNodes.Descendants(_neighbourhood).OfType<CollisionObject3D>().ToList();
        var paths = string.Join(", ", collisions.Select(c => c.GetPath()));
        AssertThat(collisions.Count).OverrideFailureMessage($"scenery has collision: {paths}").IsEqual(0);

        foreach (var visual in SceneNodes.VisibleVisuals(_neighbourhood))
        {
            var footprint = SceneNodes.Footprint(visual);
            AssertThat(footprint.Intersects(NeighbourhoodBounds.ShopAndStreet))
                .OverrideFailureMessage($"{visual.GetPath()} at {footprint} intrudes the shop and street footprint")
                .IsFalse();
            AssertThat(footprint.Intersects(NeighbourhoodBounds.Workshop))
                .OverrideFailureMessage($"{visual.GetPath()} at {footprint} intrudes the workshop footprint")
                .IsFalse();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PropsKeepClearOfCustomerMarkers()
    {
        var propVisuals = SceneNodes.VisibleVisuals(SceneNodes.Require(_neighbourhood, "Props")).ToList();

        foreach (var markerName in CustomerMarkerNames)
        {
            var marker = SceneNodes.Require(_shop, $"World/Markers/{markerName}");
            var markerX = marker.GlobalPosition.X;
            foreach (var visual in propVisuals)
            {
                var footprint = SceneNodes.Footprint(visual);
                var gap = markerX < footprint.Position.X
                    ? footprint.Position.X - markerX
                    : markerX > footprint.End.X ? markerX - footprint.End.X : 0f;
                AssertThat(gap >= PropClearanceX)
                    .OverrideFailureMessage(
                        $"{visual.GetPath()} is {gap} m from {marker.GetPath()} in x, under {PropClearanceX} m")
                    .IsTrue();
            }
        }
    }
}
