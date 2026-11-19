using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
public class OpeningTimeTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void EaseStartsAndEndsAtRestAndIsClamped()
    {
        AssertThat(OpeningTime.Ease(-1f)).IsEqual(0f);
        AssertThat(OpeningTime.Ease(0.5f)).IsEqual(0.5f);
        AssertThat(OpeningTime.Ease(2f)).IsEqual(1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FractionRunsFromZeroAtTheStartToOneAtTheEndAndIsClamped()
    {
        AssertThat(OpeningTime.Fraction(1f, 2f, 4f)).IsEqual(0f);
        AssertThat(OpeningTime.Fraction(4f, 2f, 4f)).IsEqual(0.5f);
        AssertThat(OpeningTime.Fraction(9f, 2f, 4f)).IsEqual(1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASpanOfNoLengthIsDoneOnceItHasBegun()
    {
        AssertThat(OpeningTime.Fraction(1.9f, 2f, 0f)).IsEqual(0f);
        AssertThat(OpeningTime.Fraction(2f, 2f, 0f)).IsEqual(1f);
    }
}
