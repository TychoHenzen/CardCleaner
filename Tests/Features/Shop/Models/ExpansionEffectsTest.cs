using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class ExpansionEffectsTest
{
    private static readonly ExpansionLot LotA = new("lot_a", 1, 100, "unlock_a", 40, 6, "Lot A", -8f);
    private static readonly ExpansionLot LotB = new("lot_b", 2, 200, "unlock_b", 60, 9, "Lot B", -12f);

    private static ExpansionPlan Table(params string[] owned) =>
        new(new[] { LotA, LotB }, new HashSet<string>(owned));

    [TestCase]
    [TestCategory("Unit")]
    public static void NothingOwnedGivesNoRentAndNoCustomers()
    {
        var plan = Table();

        AssertThat(ExpansionEffects.DailyRent(plan)).IsEqual(0);
        AssertThat(ExpansionEffects.ExtraCustomers(plan)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OneOwnedLotGivesItsOwnValues()
    {
        var plan = Table("lot_a");

        AssertThat(ExpansionEffects.DailyRent(plan)).IsEqual(40);
        AssertThat(ExpansionEffects.ExtraCustomers(plan)).IsEqual(6);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BothOwnedLotsGiveTheSums()
    {
        var plan = Table("lot_a", "lot_b");

        AssertThat(ExpansionEffects.DailyRent(plan)).IsEqual(100);
        AssertThat(ExpansionEffects.ExtraCustomers(plan)).IsEqual(15);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OwnedIdMissingFromTheTableIsIgnored()
    {
        var plan = Table("ghost");

        AssertThat(ExpansionEffects.DailyRent(plan)).IsEqual(0);
        AssertThat(ExpansionEffects.ExtraCustomers(plan)).IsEqual(0);
    }
}
