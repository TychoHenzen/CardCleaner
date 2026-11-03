using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class CheckoutSessionTest
{
    private static IReadOnlyList<CheckoutItem> TwoItems() =>
        new[] { new CheckoutItem(1, 7), new CheckoutItem(2, 4) };

    [TestCase]
    [TestCategory("Unit")]
    public static void TotalIsTheSumOfTheAskingPrices()
    {
        var session = new CheckoutSession(TwoItems(), PaymentMethod.Card);

        AssertThat(session.Total).IsEqual(11);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ScanMarksAnItemOnceAndRejectsUnknownAndRepeatedIds()
    {
        var session = new CheckoutSession(TwoItems(), PaymentMethod.Card);

        AssertThat(session.Scan(1)).IsTrue();
        AssertThat(session.Scan(1)).IsFalse();
        AssertThat(session.Scan(99)).IsFalse();
        AssertThat(session.ScannedCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void StateIsAwaitingPaymentOnlyOnceEveryItemIsScanned()
    {
        var session = new CheckoutSession(TwoItems(), PaymentMethod.Card);

        AssertThat(session.State).IsEqual(CheckoutState.Scanning);
        session.Scan(1);
        AssertThat(session.State).IsEqual(CheckoutState.Scanning);
        session.Scan(2);
        AssertThat(session.State).IsEqual(CheckoutState.AwaitingPayment);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CardPaymentBeforeEveryScanReturnsFalseAndStaysScanning()
    {
        var session = new CheckoutSession(TwoItems(), PaymentMethod.Card);
        session.Scan(1);

        AssertThat(session.TryPayCard(11)).IsFalse();
        AssertThat(session.State).IsEqual(CheckoutState.Scanning);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void WrongCardTotalReturnsFalseAndStaysAwaitingPayment()
    {
        var session = new CheckoutSession(TwoItems(), PaymentMethod.Card);
        session.Scan(1);
        session.Scan(2);

        AssertThat(session.TryPayCard(10)).IsFalse();
        AssertThat(session.State).IsEqual(CheckoutState.AwaitingPayment);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RightCardTotalPaysOnce()
    {
        var session = new CheckoutSession(TwoItems(), PaymentMethod.Card);
        session.Scan(1);
        session.Scan(2);

        AssertThat(session.TryPayCard(11)).IsTrue();
        AssertThat(session.State).IsEqual(CheckoutState.Paid);
        AssertThat(session.TryPayCard(11)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CashPaymentWithTheRightChangePaysOnce()
    {
        var session = new CheckoutSession(TwoItems(), PaymentMethod.Cash);
        session.Scan(1);
        session.Scan(2);

        AssertThat(session.Tendered).IsEqual(15);
        AssertThat(session.TryPayCash(new[] { 1, 1, 1, 1 })).IsTrue();
        AssertThat(session.State).IsEqual(CheckoutState.Paid);
        AssertThat(session.TryPayCash(new[] { 1, 1, 1, 1 })).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CashPaymentWithWrongChangeStaysAwaitingPayment()
    {
        var session = new CheckoutSession(TwoItems(), PaymentMethod.Cash);
        session.Scan(1);
        session.Scan(2);

        AssertThat(session.TryPayCash(new[] { 5 })).IsFalse();
        AssertThat(session.State).IsEqual(CheckoutState.AwaitingPayment);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PaymentWithTheWrongMethodReturnsFalse()
    {
        var card = new CheckoutSession(TwoItems(), PaymentMethod.Card);
        card.Scan(1);
        card.Scan(2);
        AssertThat(card.TryPayCash(new[] { 1, 1, 1, 1 })).IsFalse();

        var cash = new CheckoutSession(TwoItems(), PaymentMethod.Cash);
        cash.Scan(1);
        cash.Scan(2);
        AssertThat(cash.TryPayCard(11)).IsFalse();
        AssertThat(cash.State).IsEqual(CheckoutState.AwaitingPayment);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyBasketThrowsArgumentException()
    {
        AssertThrown(() => new CheckoutSession(Array.Empty<CheckoutItem>(), PaymentMethod.Card))
            .IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NonPositivePriceThrowsArgumentOutOfRangeException()
    {
        AssertThrown(() => new CheckoutSession(new[] { new CheckoutItem(1, 0) }, PaymentMethod.Card))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DuplicateIdsThrowArgumentException()
    {
        AssertThrown(() => new CheckoutSession(
                new[] { new CheckoutItem(1, 3), new CheckoutItem(1, 4) }, PaymentMethod.Card))
            .IsInstanceOf<ArgumentException>();
    }
}
