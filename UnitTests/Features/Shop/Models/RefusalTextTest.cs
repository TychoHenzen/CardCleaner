using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class RefusalTextTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void PriceTooHighTextIsPriceTooHigh()
    {
        AssertThat(RefusalText.For(RefusalReason.PriceTooHigh)).IsEqual("price too high");
        AssertThat(RefusalText.For(RefusalReason.PriceTooHigh)).IsEqual(RefusalText.PriceTooHigh);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NothingInStockTextIsNothingInStock()
    {
        AssertThat(RefusalText.For(RefusalReason.NothingInStock)).IsEqual("nothing in stock");
        AssertThat(RefusalText.For(RefusalReason.NothingInStock)).IsEqual(RefusalText.NothingInStock);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownReasonThrows()
    {
        AssertThrown(() => RefusalText.For((RefusalReason)99)).IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
