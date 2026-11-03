using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class ShopProgressionTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void FreshProgressionStartsAtLevelOneWithNoXp()
    {
        var progression = AutoFree(new ShopProgression());

        AssertThat(progression.Xp).IsEqual(0);
        AssertThat(progression.Level).IsEqual(1);
        AssertThat(progression.Progress).IsEqual(0f);
        AssertBool(progression.AtMaxLevel).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AwardingACheckoutAddsItsXpAndRaisesProgressChangedOnce()
    {
        var progression = AutoFree(new ShopProgression());
        var changes = 0;
        progression.ProgressChanged += () => changes++;

        progression.AwardCheckout(500);

        AssertThat(progression.Xp).IsEqual(50);
        AssertThat(progression.Level).IsEqual(2);
        AssertThat(changes).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ANonPositiveCheckoutEarnsNothingAndRaisesNothing()
    {
        var progression = AutoFree(new ShopProgression());
        var changes = 0;
        progression.ProgressChanged += () => changes++;

        progression.AwardCheckout(0);
        progression.AwardCheckout(-5);

        AssertThat(progression.Xp).IsEqual(0);
        AssertThat(changes).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LevelTwoItemUnlocksExactlyAtItsThreshold()
    {
        var progression = AutoFree(new ShopProgression());

        progression.AwardCheckout(490);
        AssertBool(progression.IsUnlocked("bulk_packing_station")).IsFalse();

        progression.AwardCheckout(10);
        AssertBool(progression.IsUnlocked("bulk_packing_station")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UnknownIdIsLockedAndHasNoRequiredLevel()
    {
        var progression = AutoFree(new ShopProgression());

        AssertBool(progression.IsUnlocked("nope")).IsFalse();
        AssertThat(progression.RequiredLevel("nope")).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LevelOneItemIsUnlockedFromTheStart()
    {
        var progression = AutoFree(new ShopProgression());

        AssertThat(progression.RequiredLevel("shelf")).IsEqual(1);
        AssertBool(progression.IsUnlocked("shelf")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ReachingTheTopLevelStopsTheCurveThere()
    {
        var progression = AutoFree(new ShopProgression());

        progression.AwardCheckout(5000);
        AssertThat(progression.Xp).IsEqual(500);
        AssertBool(progression.AtMaxLevel).IsTrue();

        progression.AwardCheckout(5000);
        AssertThat(progression.Level).IsEqual(5);
    }
}
