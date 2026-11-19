using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Portal.Models;

namespace CardCleaner.Tests.Features.Portal.Models;

[TestSuite]
public class SeamTriggerRuleTest
{
    private static CardSignature Special()
    {
        var signature = new CardSignature();
        signature = signature.With(3, 0.4f);
        return signature;
    }

    [TestCase]
    public static void SpecialCardInTheBackofficeShowsTheSeam()
    {
        AssertThat(SeamTriggerRule.ShouldShow(true, [Special()])).IsTrue();
    }

    [TestCase]
    public static void SpecialCardOutsideTheBackofficeKeepsTheSeamHidden()
    {
        AssertThat(SeamTriggerRule.ShouldShow(false, [Special()])).IsFalse();
    }

    [TestCase]
    public static void CommonCardsAndEmptyHandsKeepTheSeamHidden()
    {
        AssertThat(SeamTriggerRule.ShouldShow(true, [new CardSignature(), new CardSignature()])).IsFalse();
        AssertThat(SeamTriggerRule.ShouldShow(true, [])).IsFalse();
    }

    [TestCase]
    public static void OneSpecialCardAmongCommonOnesIsEnough()
    {
        AssertThat(SeamTriggerRule.ShouldShow(true, [new CardSignature(), Special(), new CardSignature()])).IsTrue();
    }

}
