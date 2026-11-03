using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
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
    public static void UnknownReasonThrows()
    {
        AssertThrown(() => RefusalText.For((RefusalReason)99)).IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
