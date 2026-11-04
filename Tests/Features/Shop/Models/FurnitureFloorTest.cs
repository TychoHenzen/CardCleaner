using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class FurnitureFloorTest
{
    private static readonly ExpansionLot LotA = new("lot_a", 1, 100, "unlock_a", 40, 6, "Lot A", -8f);
    private static readonly ExpansionLot LotB = new("lot_b", 2, 200, "unlock_b", 60, 9, "Lot B", -12f);

    private static ExpansionPlan Table(params string[] owned) =>
        new(new[] { LotA, LotB }, new HashSet<string>(owned));

    [TestCase]
    [TestCategory("Unit")]
    public static void NothingOwnedIsTheBaseShopFloor()
    {
        var floor = FurnitureFloor.For(Table());

        AssertThat(floor.MinX).IsEqual(0.5f);
        AssertThat(floor.MinZ).IsEqual(0.5f);
        AssertThat(floor.MaxX).IsEqual(15.5f);
        AssertThat(floor.MaxZ).IsEqual(11.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OwnedLotWidensOnlyTheMinimumX()
    {
        var floor = FurnitureFloor.For(Table("lot_a"));

        AssertThat(floor.MinX).IsEqual(-7.5f);
        AssertThat(floor.MinZ).IsEqual(0.5f);
        AssertThat(floor.MaxX).IsEqual(15.5f);
        AssertThat(floor.MaxZ).IsEqual(11.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoOwnedLotsTakeTheSmallestStorefrontEdge()
    {
        var floor = FurnitureFloor.For(Table("lot_a", "lot_b"));

        AssertThat(floor.MinX).IsEqual(-11.5f);
        AssertThat(floor.MaxX).IsEqual(15.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void APointInsideAnOwnedLotIsValidOnlyAfterItIsOwned()
    {
        var footprint = new FurnitureFootprint(2f, 1f);
        var pose = new FurniturePose(-4f, 6f, 0);
        var none = new List<FurnitureRect>();

        AssertThat(FurniturePlacementModel.IsValid(footprint, pose, none, FurnitureFloor.For(Table())))
            .IsFalse();
        AssertThat(FurniturePlacementModel.IsValid(footprint, pose, none, FurnitureFloor.For(Table("lot_a"))))
            .IsTrue();
    }
}
