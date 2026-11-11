using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// <see cref="ShopNavigationRig.WaitForBakeSync" /> is bounded by wall-clock time. A bake that reaches the server in
/// time returns normally; a region that never changes ends in an exception whose message names the wait, both ids
/// and the physics frames that passed.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopNavigationRigTest
{
    private const string RegionPath = "World/Navigation";
    private const double ShortBoundSeconds = 0.5;

    private ShopNavigationRegion _region = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _region = shop.GetNode<ShopNavigationRegion>(RegionPath);

        // The first bake runs inside _Ready during AddNode, so read the region id before adding the scene.
        uint regionBefore = RegionId();
        AddNode(shop);
        await ShopNavigationRig.WaitForBakeSync(_region, regionBefore);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ARebakeInsideTheBoundReturnsNormally()
    {
        uint regionBefore = RegionId();
        _region.Rebake();

        await ShopNavigationRig.WaitForBakeSync(_region, regionBefore);

        AssertThat(RegionId()).IsNotEqual(regionBefore);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ABakeThatIsNeverAppliedThrowsAMessageNamingTheWaitBothIdsAndThePhysicsFrames()
    {
        uint regionId = RegionId();

        var thrown = await Capture(() => ShopNavigationRig.WaitForBakeSync(_region, regionId, ShortBoundSeconds));

        AssertThat(thrown).IsNotNull();
        var message = thrown!.Message;
        AssertBool(message.StartsWith("navigation did not sync", StringComparison.Ordinal)).IsTrue();
        AssertThat(message).Contains("Timed out after");
        AssertThat(message).Contains("waiting for: the navigation map to apply a bake");
        AssertThat(message).Contains("Physics frames passed:");
        AssertThat(message).Contains($"Region id now {regionId} (before {regionId})");
        AssertThat(message).Contains("map id now");
        AssertThat(message).Contains("before the region changed");
    }

    private uint RegionId() => NavigationServer3D.RegionGetIterationId(_region.GetRid());

    private static async Task<InvalidOperationException?> Capture(Func<Task> wait)
    {
        try
        {
            await wait();
            return null;
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
    }
}
