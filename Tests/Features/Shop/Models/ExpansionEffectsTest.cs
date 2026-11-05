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
    public static void NothingOwnedGivesNoCustomers() =>
        AssertThat(ExpansionEffects.ExtraCustomers(Table())).IsEqual(0);

    [TestCase]
    [TestCategory("Unit")]
    public static void OneOwnedLotGivesItsOwnValue() =>
        AssertThat(ExpansionEffects.ExtraCustomers(Table("lot_a"))).IsEqual(6);

    [TestCase]
    [TestCategory("Unit")]
    public static void BothOwnedLotsGiveTheSum() =>
        AssertThat(ExpansionEffects.ExtraCustomers(Table("lot_a", "lot_b"))).IsEqual(15);

    [TestCase]
    [TestCategory("Unit")]
    public static void OwnedIdMissingFromTheTableIsIgnored() =>
        AssertThat(ExpansionEffects.ExtraCustomers(Table("ghost"))).IsEqual(0);
}
