using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class CheckoutCounterTextTest
{
    private static CheckoutSession Sale(PaymentMethod method) =>
        new(new[] { new CheckoutItem(0, 5), new CheckoutItem(1, 7) }, method);

    [TestCase]
    [TestCategory("Unit")]
    public static void AnIdleCounterSaysOnlyItsLastMessage()
    {
        AssertThat(CheckoutCounterText.For(null, false, string.Empty)).IsEqual(string.Empty);
        AssertThat(CheckoutCounterText.For(null, false, "Paid 12")).IsEqual("Paid 12");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ScanningShowsTheProgressAndThenTheMessage()
    {
        var sale = Sale(PaymentMethod.Card);
        sale.Scan(0);

        AssertThat(CheckoutCounterText.For(sale, false, string.Empty)).IsEqual("Scan the items: 1 of 2");
        AssertThat(CheckoutCounterText.For(sale, false, "Paid 9")).IsEqual("Scan the items: 1 of 2\nPaid 9");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AScannedSaleNamesTheTotalToTake()
    {
        var card = Sale(PaymentMethod.Card);
        card.Scan(0);
        card.Scan(1);
        var cash = Sale(PaymentMethod.Cash);
        cash.Scan(0);
        cash.Scan(1);

        AssertThat(CheckoutCounterText.For(card, false, string.Empty)).IsEqual("Card payment: 12");
        AssertThat(CheckoutCounterText.For(cash, false, "Wrong change"))
            .IsEqual($"Cash payment: 12, handed {cash.Tendered}\nWrong change");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AClaimedSaleSaysTheCashierServes()
    {
        AssertThat(CheckoutCounterText.For(Sale(PaymentMethod.Card), true, string.Empty))
            .IsEqual(CheckoutCounterText.CashierServing);
    }
}
