using System;
using CardCleaner.Scripts.Features.Shop.Checkout;

namespace CardCleaner.Tests.Features.Shop.Checkout;

[TestSuite]
public class PaymentRulesTest
{
    [TestCase]
    public static void CashTenderedRoundsTheTotalUpToTheNextMultipleOfFive()
    {
        AssertThat(PaymentRules.CashTendered(7)).IsEqual(10);
        AssertThat(PaymentRules.CashTendered(10)).IsEqual(10);
        AssertThat(PaymentRules.CashTendered(11)).IsEqual(15);
        AssertThat(PaymentRules.CashTendered(101)).IsEqual(105);
    }

    [TestCase]
    public static void ChangeIsTenderedMinusTotal()
    {
        AssertThat(PaymentRules.Change(15, 11)).IsEqual(4);
    }

    [TestCase]
    public static void CardTotalMatchesOnlyTheExactTotal()
    {
        AssertThat(PaymentRules.CardTotalMatches(11, 11)).IsTrue();
        AssertThat(PaymentRules.CardTotalMatches(11, 10)).IsFalse();
    }

    [TestCase]
    public static void ChangeMatchesSumsOfChangeDenominations()
    {
        AssertThat(PaymentRules.ChangeMatches(new[] { 1, 1, 1, 1 }, 15, 11)).IsTrue();
        AssertThat(PaymentRules.ChangeMatches(new[] { 5 }, 15, 11)).IsFalse();
        AssertThat(PaymentRules.ChangeMatches(Array.Empty<int>(), 10, 10)).IsTrue();
    }

    [TestCase]
    public static void ChangeMatchesRejectsAPickThatIsNotADenomination()
    {
        AssertThat(PaymentRules.ChangeMatches(new[] { 2, 2 }, 15, 11)).IsFalse();
    }

    [TestCase]
    public static void NonPositiveTotalThrows()
    {
        AssertThrown(() => PaymentRules.CashTendered(0)).IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => PaymentRules.CardTotalMatches(1, 0)).IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
