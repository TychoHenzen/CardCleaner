using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Portal.Models;

namespace CardCleaner.Tests.Features.Portal.Models;

[TestSuite]
[RequireGodotRuntime]
public class SeamTriggerRuleTest
{
    private static CardSignature Special()
    {
        var signature = new CardSignature();
        signature[3] = 0.4f;
        return signature;
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialCardInTheBackofficeShowsTheSeam()
    {
        AssertThat(SeamTriggerRule.ShouldShow(true, [Special()])).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpecialCardOutsideTheBackofficeKeepsTheSeamHidden()
    {
        AssertThat(SeamTriggerRule.ShouldShow(false, [Special()])).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CommonCardsAndEmptyHandsKeepTheSeamHidden()
    {
        AssertThat(SeamTriggerRule.ShouldShow(true, [new CardSignature(), new CardSignature()])).IsFalse();
        AssertThat(SeamTriggerRule.ShouldShow(true, [])).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OneSpecialCardAmongCommonOnesIsEnough()
    {
        AssertThat(SeamTriggerRule.ShouldShow(true, [new CardSignature(), Special(), new CardSignature()])).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CardWithoutASignatureCountsAsCommon()
    {
        AssertThat(SeamTriggerRule.ShouldShow(true, [null])).IsFalse();
    }
}
