using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Navigation maps build their iterations on the main thread, inside the navigation sync. With async map iterations
/// (the Godot default) a build that finishes in the same sync as the region change and a stale build that finishes
/// there look the same from outside, so neither <see cref="Scripts.Features.Shop.Components.ShopNavigationRegion" />'s
/// <c>MapForceUpdate</c> nor <see cref="ShopNavigationRig.WaitForBakeSync" /> can tell when a rebake has reached the
/// map. Built synchronously, the map holds the new region iteration on the sync that applies it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NavigationMapSyncSettingTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void MapIterationsAreNotAsync()
    {
        var async = ProjectSettings.GetSetting("navigation/world/map_use_async_iterations").AsBool();

        Assertions.AssertBool(async)
            .OverrideFailureMessage(
                "navigation/world/map_use_async_iterations is on; a rebake can no longer be awaited")
            .IsFalse();
    }
}
