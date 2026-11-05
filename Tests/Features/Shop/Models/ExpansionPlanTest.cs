using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class ExpansionPlanTest
{
    private static readonly ExpansionLot LotA = new("lot_a", 1, 100, "unlock_a", 40, 6, "Lot A", -8f);
    private static readonly ExpansionLot LotB = new("lot_b", 2, 200, "unlock_b", 60, 9, "Lot B", -12f);

    private static ExpansionPlan Table(params string[] owned) =>
        new(new[] { LotB, LotA }, new HashSet<string>(owned));

    [TestCase]
    [TestCategory("Unit")]
    public static void NextIsTheFirstUnownedLotByOrder()
    {
        var next = Table().Next;

        AssertThat(next).IsNotNull();
        AssertThat(next!.Id).IsEqual("lot_a");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyTableHasNoNext()
    {
        var plan = new ExpansionPlan(Array.Empty<ExpansionLot>(), new HashSet<string>());

        AssertThat(plan.Next).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LaterLotIsOutOfOrderWhileAnEarlierOneIsUnowned()
    {
        AssertThat(Table().Decide("lot_b", true, 1000)).IsEqual(PurchaseOutcome.OutOfOrder);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LockedLotIsRefused()
    {
        AssertThat(Table().Decide("lot_a", false, 1000)).IsEqual(PurchaseOutcome.Locked);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnaffordableLotIsRefused()
    {
        AssertThat(Table().Decide("lot_a", true, 99)).IsEqual(PurchaseOutcome.Unaffordable);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AffordableLotIsBoughtAndThePlanIsImmutable()
    {
        var plan = Table();

        AssertThat(plan.Decide("lot_a", true, 100)).IsEqual(PurchaseOutcome.Bought);
        AssertBool(plan.Owned.Contains("lot_a")).IsFalse();

        var bought = plan.With("lot_a");
        AssertBool(bought.Owned.Contains("lot_a")).IsTrue();
        AssertBool(plan.Owned.Contains("lot_a")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NextMovesToTheFollowingLotOnceOneIsOwned()
    {
        var next = Table("lot_a").Next;

        AssertThat(next).IsNotNull();
        AssertThat(next!.Id).IsEqual("lot_b");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OwnedLotIsAlreadyOwned()
    {
        AssertThat(Table("lot_a").Decide("lot_a", true, 1000)).IsEqual(PurchaseOutcome.AlreadyOwned);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownIdIsUnknown()
    {
        AssertThat(Table().Decide("nope", true, 1000)).IsEqual(PurchaseOutcome.Unknown);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DuplicateOrderThrowsNamingTheOrder()
    {
        var twin = new ExpansionLot("lot_c", 1, 300, "unlock_c", 10, 1, "Lot C", -4f);

        AssertThrown(() => new ExpansionPlan(new[] { LotA, twin }, new HashSet<string>()))
            .IsInstanceOf<ArgumentException>();
    }
}
