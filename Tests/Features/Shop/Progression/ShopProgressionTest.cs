using Godot;
using CardCleaner.Scripts.Features.Shop.Progression;

namespace CardCleaner.Tests.Features.Shop.Progression;

[TestSuite]
[RequireGodotRuntime]
public class ShopProgressionTest
{
    [TestCase]
    public static void FreshProgressionStartsAtLevelOneWithNoXp()
    {
        var progression = AutoFree(new ShopProgression());

        AssertThat(progression.Xp).IsEqual(0);
        AssertThat(progression.Level).IsEqual(1);
        AssertThat(progression.Progress).IsEqual(0f);
        AssertBool(progression.AtMaxLevel).IsFalse();
    }

    [TestCase]
    public static void AwardingACheckoutAddsItsXpAndRaisesProgressChangedOnce()
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
    public static void ANonPositiveCheckoutEarnsNothingAndRaisesNothing()
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
    public static void LevelTwoItemUnlocksExactlyAtItsThreshold()
    {
        var progression = AutoFree(new ShopProgression());

        progression.AwardCheckout(490);
        AssertBool(progression.IsUnlocked("bulk_packing_station")).IsFalse();

        progression.AwardCheckout(10);
        AssertBool(progression.IsUnlocked("bulk_packing_station")).IsTrue();
    }

    [TestCase]
    public static void UnknownIdIsLockedAndHasNoRequiredLevel()
    {
        var progression = AutoFree(new ShopProgression());

        AssertBool(progression.IsUnlocked("nope")).IsFalse();
        AssertThat(progression.RequiredLevel("nope")).IsNull();
    }

    [TestCase]
    public static void LevelOneItemIsUnlockedFromTheStart()
    {
        var progression = AutoFree(new ShopProgression());

        AssertThat(progression.RequiredLevel("shelf_unit")).IsEqual(1);
        AssertBool(progression.IsUnlocked("shelf_unit")).IsTrue();
    }

    [TestCase]
    public static void ReachingTheTopLevelStopsTheCurveThere()
    {
        var progression = AutoFree(new ShopProgression());

        progression.AwardCheckout(5000);
        AssertThat(progression.Xp).IsEqual(500);
        AssertBool(progression.AtMaxLevel).IsTrue();

        progression.AwardCheckout(5000);
        AssertThat(progression.Level).IsEqual(5);
    }
}
