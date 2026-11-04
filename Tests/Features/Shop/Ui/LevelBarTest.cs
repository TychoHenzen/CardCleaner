using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

[TestSuite]
[RequireGodotRuntime]
public class LevelBarTest
{
    private const int CheckoutAmount = 250;
    private const int MaxLevelAmount = 5000;

    [TestCase]
    [TestCategory("Unit")]
    public void ShowsLevelOneAtZeroXp()
    {
        var progression = AutoFree(new ShopProgression());
        var bar = new LevelBar();
        AddNode(bar);
        bar.Progression = progression;

        AssertThat(bar.LevelLabel!.Text).IsEqual("Level 1");
        AssertThat(Mathf.IsEqualApprox(bar.Bar!.Value, 0.0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FillsTowardTheNextLevelAndRollsOver()
    {
        var progression = AutoFree(new ShopProgression());
        var bar = new LevelBar();
        AddNode(bar);
        bar.Progression = progression;

        progression.AwardCheckout(CheckoutAmount);

        AssertThat(bar.LevelLabel!.Text).IsEqual("Level 1");
        AssertThat(Mathf.IsEqualApprox(bar.Bar!.Value, 0.5)).IsTrue();

        progression.AwardCheckout(CheckoutAmount);

        AssertThat(bar.LevelLabel!.Text).IsEqual("Level 2");
        AssertThat(Mathf.IsEqualApprox(bar.Bar!.Value, 0.0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TopLevelShowsMaxAndAFullBar()
    {
        var progression = AutoFree(new ShopProgression());
        var bar = new LevelBar();
        AddNode(bar);
        bar.Progression = progression;

        progression.AwardCheckout(MaxLevelAmount);

        AssertThat(bar.LevelLabel!.Text).IsEqual("Level 5 (max)");
        AssertThat(Mathf.IsEqualApprox(bar.Bar!.Value, 1.0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WithoutProgressionShowsADash()
    {
        var bar = new LevelBar();
        AddNode(bar);

        AssertThat(bar.LevelLabel!.Text).IsEqual("Level -");
        AssertThat(Mathf.IsEqualApprox(bar.Bar!.Value, 0.0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StopsFollowingProgressionOnceItLeavesTheTree()
    {
        var progression = AutoFree(new ShopProgression());
        // Not auto-freed: the bar is removed from the tree mid-test and freed by hand afterwards.
        var bar = AddNode(new LevelBar(), autoFree: false);
        bar.Progression = progression;

        bar.GetParent()?.RemoveChild(bar);
        progression.AwardCheckout(MaxLevelAmount);
        var label = bar.LevelLabel!.Text;
        bar.Free();

        AssertThat(label).IsEqual("Level 1");
    }
}
