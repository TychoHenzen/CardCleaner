using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
public class HandFanTest
{
    private const float Spread = 45f;
    private const int Fill = 10;

    [TestCase]
    public static void OneCardStandsUpright()
    {
        AssertThat(HandFan.AngleDegrees(0, 1, Spread, Fill)).IsEqual(0f);
    }

    [TestCase]
    public static void UntilTheFanIsFullEveryCardTurnsByTheSameStep()
    {
        for (var count = 2; count <= Fill; count++)
            AssertThat(HandFan.StepDegrees(count, Spread, Fill)).IsEqualApprox(5f, 1e-4f);
    }

    [TestCase]
    public static void TheFillCountFillsTheWholeSpread()
    {
        AssertThat(HandFan.AngleDegrees(0, Fill, Spread, Fill)).IsEqualApprox(-22.5f, 1e-4f);
        AssertThat(HandFan.AngleDegrees(Fill - 1, Fill, Spread, Fill)).IsEqualApprox(22.5f, 1e-4f);
    }

    [TestCase]
    public static void MoreCardsKeepTheSpreadAndCloseTheGaps()
    {
        AssertThat(HandFan.StepDegrees(19, Spread, Fill)).IsEqualApprox(2.5f, 1e-4f);
        AssertThat(HandFan.AngleDegrees(0, 19, Spread, Fill)).IsEqualApprox(-22.5f, 1e-4f);
        AssertThat(HandFan.AngleDegrees(18, 19, Spread, Fill)).IsEqualApprox(22.5f, 1e-4f);
    }

    [TestCase]
    public static void AFanIsCentredAndRunsLeftToRight()
    {
        AssertThat(HandFan.AngleDegrees(0, 4, Spread, Fill)).IsEqualApprox(-7.5f, 1e-4f);
        AssertThat(HandFan.AngleDegrees(1, 4, Spread, Fill)).IsEqualApprox(-2.5f, 1e-4f);
        AssertThat(HandFan.AngleDegrees(2, 4, Spread, Fill)).IsEqualApprox(2.5f, 1e-4f);
        AssertThat(HandFan.AngleDegrees(3, 4, Spread, Fill)).IsEqualApprox(7.5f, 1e-4f);
    }
}
