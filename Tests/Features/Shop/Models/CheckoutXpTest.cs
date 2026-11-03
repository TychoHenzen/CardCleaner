using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class CheckoutXpTest
{
    [TestCase(10, 1)]
    [TestCase(5, 1)]
    [TestCase(7, 1)]
    [TestCase(95, 9)]
    [TestCase(100, 10)]
    [TestCase(0, 0)]
    [TestCase(-3, 0)]
    [TestCategory("Unit")]
    public static void XpIsOnePerTenPaidWithAtLeastOneForAnySale(int amountPaid, int expectedXp)
    {
        AssertThat(CheckoutXp.For(amountPaid)).IsEqual(expectedXp);
    }
}
