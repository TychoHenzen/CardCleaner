using System;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class PaymentRulesChangePartsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Zero_due_is_empty()
    {
        AssertThat(PaymentRules.ChangeParts(0).Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Parts_sum_to_the_due_for_every_due_from_0_to_200()
    {
        for (var due = 0; due <= 200; due++)
        {
            var parts = PaymentRules.ChangeParts(due);
            var sum = 0;
            foreach (var part in parts)
            {
                AssertThat(PaymentRules.ChangeDenominations.Contains(part)).IsTrue();
                sum += part;
            }

            AssertThat(sum).IsEqual(due);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Largest_first()
    {
        AssertThat(string.Join(",", PaymentRules.ChangeParts(94))).IsEqual("50,20,20,1,1,1,1");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Negative_due_throws()
    {
        AssertThrown(() => PaymentRules.ChangeParts(-1)).IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Parts_satisfy_ChangeMatches()
    {
        for (var total = 1; total <= 60; total++)
        {
            var tendered = PaymentRules.CashTendered(total);
            var parts = PaymentRules.ChangeParts(PaymentRules.Change(tendered, total));

            AssertThat(PaymentRules.ChangeMatches(parts, tendered, total)).IsTrue();
        }
    }
}
