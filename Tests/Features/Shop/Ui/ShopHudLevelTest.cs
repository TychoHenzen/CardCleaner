using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

[TestSuite]
[RequireGodotRuntime]
public class ShopHudLevelTest
{
    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
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
