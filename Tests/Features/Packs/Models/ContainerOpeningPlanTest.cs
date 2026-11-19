using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
public class ContainerOpeningPlanTest
{
    private const int Items = 8;

    [TestCase]
    [TestCategory("Unit")]
    public static void AContainerOnTheFloorOpensAtOnce()
    {
        var plan = ContainerOpeningPlan.For(CardContainerKind.Pack, 0f, Items);

        AssertThat(plan.Settle).IsEqual(0f);
        AssertThat(plan.SettleProgress(0f)).IsEqual(1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AFallTakesAsLongAsAFreeFallButNeverLongerThanTheLongestFall()
    {
        AssertThat(ContainerOpeningPlan.FallSeconds(1.2f)).IsEqualApprox(0.4949f, 0.001f);
        AssertThat(ContainerOpeningPlan.FallSeconds(30f)).IsEqual(0.7f);
        AssertThat(ContainerOpeningPlan.FallSeconds(-1f)).IsEqual(0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheSealOpensAfterTheFallAndTheItemsLeaveAfterTheSeal()
    {
        var plan = ContainerOpeningPlan.For(CardContainerKind.Pack, 1.2f, Items);

        AssertThat(plan.UnsealProgress(plan.Settle)).IsEqual(0f);
        AssertThat(plan.UnsealProgress(plan.Settle + plan.Unseal)).IsEqual(1f);
        AssertThat(plan.ReleaseAt(0)).IsEqual(plan.Settle + plan.Unseal);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ItemsLeaveOneStepApartAndEachRisesForTheRiseTime()
    {
        var plan = ContainerOpeningPlan.For(CardContainerKind.Booster, 0f, Items);

        for (var i = 1; i < Items; i++)
            AssertThat(plan.ReleaseAt(i) - plan.ReleaseAt(i - 1)).IsEqualApprox(plan.ReleaseStep, 0.0001f);
        AssertThat(plan.RiseProgress(3, plan.ReleaseAt(3))).IsEqual(0f);
        AssertThat(plan.RiseProgress(3, plan.ReleaseAt(3) + plan.Rise / 2f)).IsEqualApprox(0.5f, 0.0001f);
        AssertThat(plan.RiseProgress(3, plan.ReleaseAt(3) + plan.Rise)).IsEqual(1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheWrapperShrinksOnlyOnceTheLastItemHasRisen()
    {
        var plan = ContainerOpeningPlan.For(CardContainerKind.Pack, 0.5f, Items);

        AssertThat(plan.VanishStart).IsEqualApprox(plan.ReleaseAt(Items - 1) + plan.Rise, 0.0001f);
        AssertThat(plan.VanishProgress(plan.VanishStart)).IsEqual(0f);
        AssertThat(plan.VanishProgress(plan.Duration)).IsEqual(1f);
        AssertThat(plan.Duration).IsEqualApprox(plan.VanishStart + plan.Vanish, 0.0001f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnEmptyContainerShrinksAsSoonAsItsSealIsOpen()
    {
        var plan = ContainerOpeningPlan.For(CardContainerKind.Pack, 0f, 0);

        AssertThat(plan.VanishStart).IsEqual(plan.Settle + plan.Unseal);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryOpeningEndsWithinThreeSeconds()
    {
        foreach (var kind in new[] { CardContainerKind.Pack, CardContainerKind.Booster })
            AssertThat(ContainerOpeningPlan.For(kind, 30f, Items).Duration).IsLess(3f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EaseStartsAndEndsAtRestAndIsClamped()
    {
        AssertThat(ContainerOpeningPlan.Ease(-1f)).IsEqual(0f);
        AssertThat(ContainerOpeningPlan.Ease(0.5f)).IsEqual(0.5f);
        AssertThat(ContainerOpeningPlan.Ease(2f)).IsEqual(1f);
    }
}
