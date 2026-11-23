using System;
using CardCleaner.Scripts.Features.Shop.Customers;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class RefusalTextTest
{
    [TestCase]
    public static void PriceTooHighTextIsPriceTooHigh()
    {
        AssertThat(RefusalText.For(RefusalReason.PriceTooHigh)).IsEqual("price too high");
        AssertThat(RefusalText.For(RefusalReason.PriceTooHigh)).IsEqual(RefusalText.PriceTooHigh);
    }

    [TestCase]
    public static void NothingInStockTextIsNothingInStock()
    {
        AssertThat(RefusalText.For(RefusalReason.NothingInStock)).IsEqual("nothing in stock");
        AssertThat(RefusalText.For(RefusalReason.NothingInStock)).IsEqual(RefusalText.NothingInStock);
    }

    [TestCase]
    public static void UnknownReasonThrows()
    {
        AssertThrown(() => RefusalText.For((RefusalReason)99)).IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
