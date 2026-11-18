using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class CheckoutPaymentGuardTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void No_register_outranks_everything()
    {
        AssertThat(CheckoutPaymentGuard.Evaluate(false, CheckoutState.AwaitingPayment, true))
            .IsEqual(PaymentReadiness.NoRegister);
        AssertThat(CheckoutPaymentGuard.Evaluate(false, null, null)).IsEqual(PaymentReadiness.NoRegister);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Missing_session_or_front_customer_is_no_sale()
    {
        AssertThat(CheckoutPaymentGuard.Evaluate(true, null, true)).IsEqual(PaymentReadiness.NoSale);
        AssertThat(CheckoutPaymentGuard.Evaluate(true, CheckoutState.AwaitingPayment, null))
            .IsEqual(PaymentReadiness.NoSale);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Customer_who_left_outranks_unscanned_items()
    {
        AssertThat(CheckoutPaymentGuard.Evaluate(true, CheckoutState.Scanning, false))
            .IsEqual(PaymentReadiness.CustomerLeft);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Unscanned_items_must_be_scanned_first()
    {
        AssertThat(CheckoutPaymentGuard.Evaluate(true, CheckoutState.Scanning, true))
            .IsEqual(PaymentReadiness.ScanFirst);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Scanned_sale_with_a_waiting_customer_is_ready()
    {
        AssertThat(CheckoutPaymentGuard.Evaluate(true, CheckoutState.AwaitingPayment, true))
            .IsEqual(PaymentReadiness.Ready);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Messages_name_only_the_refusals_the_player_needs()
    {
        AssertThat(CheckoutPaymentGuard.MessageFor(PaymentReadiness.NoRegister))
            .IsEqual(CheckoutPaymentGuard.UnavailableMessage);
        AssertThat(CheckoutPaymentGuard.MessageFor(PaymentReadiness.ScanFirst))
            .IsEqual(CheckoutPaymentGuard.ScanFirstMessage);
        AssertThat(CheckoutPaymentGuard.MessageFor(PaymentReadiness.NoSale)).IsNull();
        AssertThat(CheckoutPaymentGuard.MessageFor(PaymentReadiness.CustomerLeft)).IsNull();
        AssertThat(CheckoutPaymentGuard.MessageFor(PaymentReadiness.Ready)).IsNull();
    }
}
