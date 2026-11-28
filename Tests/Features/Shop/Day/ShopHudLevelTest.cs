using Godot;
using CardCleaner.Scripts.Features.Shop.Day;
using CardCleaner.Scripts.Features.Shop.Progression;

namespace CardCleaner.Tests.Features.Shop.Day;

[TestSuite]
[RequireGodotRuntime]
public class ShopHudLevelTest
{
    [TestCase]
    public static void HudCarriesTheLevelBarInItsStackAndFollowsProgression()
    {
        var hud = new ShopHud();
        AddNode(hud);
        var levelBar = hud.LevelBar!;

        AssertThat(levelBar).IsNotNull();
        AssertThat(levelBar.GetParent() is VBoxContainer).IsTrue();
        AssertThat(levelBar.LevelLabel!.Text).IsEqual("Level -");

        levelBar.Progression = AutoFree(new ShopProgression());

        AssertThat(levelBar.LevelLabel!.Text).IsEqual("Level 1");
    }

    [TestCase]
    public static void LevelBarNeverTakesMouseInput()
    {
        var hud = new ShopHud();
        AddNode(hud);
        var levelBar = hud.LevelBar!;

        AssertThat(levelBar.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        AssertThat(levelBar.LevelLabel!.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        AssertThat(levelBar.Bar!.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
    }
}
