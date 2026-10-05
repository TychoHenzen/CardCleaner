using CardCleaner.Scripts.Features.Portal.Models;

namespace CardCleaner.Tests.Features.Portal.Models;

[TestSuite]
[RequireGodotRuntime]
public class SeamPhaseRuleTest
{
    private const float Open = 3.5f;
    private const float Close = 4.5f;

    private static SeamPhase Next(SeamPhase current, bool triggered, float distance)
    {
        return SeamPhaseRule.Next(current, triggered, distance, Open, Close);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void WithoutTriggerTheSeamIsHiddenAtAnyDistance()
    {
        AssertThat(Next(SeamPhase.Hidden, false, 0.5f)).IsEqual(SeamPhase.Hidden);
        AssertThat(Next(SeamPhase.Glowing, false, 10f)).IsEqual(SeamPhase.Hidden);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TriggeredSeamGlowsFarAwayAndOpensWithinRange()
    {
        AssertThat(Next(SeamPhase.Hidden, true, 6f)).IsEqual(SeamPhase.Glowing);
        AssertThat(Next(SeamPhase.Glowing, true, 3.5f)).IsEqual(SeamPhase.Open);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DroppingTheCardClosesAnOpenSeamImmediately()
    {
        AssertThat(Next(SeamPhase.Open, false, 1f)).IsEqual(SeamPhase.Hidden);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OpenSeamStaysOpenUntilPastTheCloseRange()
    {
        AssertThat(Next(SeamPhase.Open, true, 4.0f)).IsEqual(SeamPhase.Open);
        AssertThat(Next(SeamPhase.Open, true, 4.6f)).IsEqual(SeamPhase.Glowing);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void GlowingSeamDoesNotOpenInTheHysteresisBand()
    {
        AssertThat(Next(SeamPhase.Glowing, true, 4.0f)).IsEqual(SeamPhase.Glowing);
    }
}
