using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
public class BoosterRevealPlanTest
{
    private const int Cards = 8;

    [TestCase]
    [TestCategory("Unit")]
    public static void TheBoosterComesToTheMiddleOfTheViewBeforeItsStripTears()
    {
        var plan = new BoosterRevealPlan(Cards);

        AssertThat(plan.PresentProgress(0f)).IsEqual(0f);
        AssertThat(plan.PresentProgress(BoosterRevealPlan.Present)).IsEqual(1f);
        AssertThat(plan.TearProgress(BoosterRevealPlan.Present)).IsEqual(0f);
        AssertThat(plan.TearProgress(BoosterRevealPlan.Present + BoosterRevealPlan.Tear)).IsEqual(1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheFirstCardLeavesOnceTheStripIsOffAndTheRestFollowOneStepApart()
    {
        var plan = new BoosterRevealPlan(Cards);

        AssertThat(plan.ReleaseAt(0)).IsEqual(BoosterRevealPlan.Present + BoosterRevealPlan.Tear);
        for (var i = 1; i < Cards; i++)
            AssertThat(plan.ReleaseAt(i) - plan.ReleaseAt(i - 1)).IsEqualApprox(BoosterRevealPlan.ReleaseStep, 0.0001f);
        AssertThat(plan.RiseProgress(5, plan.ReleaseAt(5))).IsEqual(0f);
        AssertThat(plan.RiseProgress(5, plan.ReleaseAt(5) + BoosterRevealPlan.Rise)).IsEqual(1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheWrapperShrinksOnceTheLastCardHasLeftIt()
    {
        var plan = new BoosterRevealPlan(Cards);

        AssertThat(plan.VanishStart).IsEqual(plan.ReleaseAt(Cards - 1));
        AssertThat(plan.VanishProgress(plan.VanishStart + BoosterRevealPlan.Vanish)).IsEqualApprox(1f, 0.0001f);
    }

    // The player sees the whole fan, every card risen to its spot, for the show time before it slides into the hand.
    [TestCase]
    [TestCategory("Unit")]
    public static void TheFanIsShownWithEveryCardInPlaceBeforeItSlidesIntoTheHand()
    {
        var plan = new BoosterRevealPlan(Cards);

        AssertThat(plan.RiseProgress(Cards - 1, plan.IntoHandStart - BoosterRevealPlan.Show)).IsEqualApprox(1f, 0.0001f);
        AssertThat(plan.IntoHandStart - plan.ReleaseAt(Cards - 1) - BoosterRevealPlan.Rise)
            .IsEqualApprox(BoosterRevealPlan.Show, 0.0001f);
        AssertThat(plan.Duration).IsEqualApprox(plan.IntoHandStart + BoosterRevealPlan.IntoHand, 0.0001f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheSlideIntoTheHandIsTimedFromWhenItBegan()
    {
        AssertThat(BoosterRevealPlan.IntoHandProgress(0f)).IsEqual(0f);
        AssertThat(BoosterRevealPlan.IntoHandProgress(BoosterRevealPlan.IntoHand / 2f)).IsEqualApprox(0.5f, 0.0001f);
        AssertThat(BoosterRevealPlan.IntoHandProgress(10f)).IsEqual(1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OpeningABoosterInHandTakesUnderTwoSeconds()
    {
        AssertThat(new BoosterRevealPlan(Cards).Duration).IsLess(2f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnEmptyBoosterVanishesOnceItsStripIsOff()
    {
        var plan = new BoosterRevealPlan(0);

        AssertThat(plan.VanishStart).IsEqual(BoosterRevealPlan.Present + BoosterRevealPlan.Tear);
        AssertThat(plan.Duration).IsEqualApprox(plan.VanishStart + BoosterRevealPlan.IntoHand, 0.0001f);
    }
}
