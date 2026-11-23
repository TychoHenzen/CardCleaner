using System.Collections.Generic;
using Godot;
using CardCleaner.Scripts.Features.Shop.Expansion;
using CardCleaner.Scripts.Features.Shop.Furniture;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class FurnitureFloorTest
{
    private static readonly ExpansionLot LotA = new("lot_a", 1, 100, "unlock_a", 40, 6, "Lot A", -8f);
    private static readonly ExpansionLot LotB = new("lot_b", 2, 200, "unlock_b", 60, 9, "Lot B", -12f);
    private static readonly FurnitureFootprint Shelf = new(2f, 1f);
    private static readonly List<FurnitureRect> None = [];

    private static ExpansionPlan Table(params string[] owned) =>
        new(new[] { LotA, LotB }, new HashSet<string>(owned));

    [TestCase]
    public static void NothingOwnedIsTheStorefrontTheBackRoomAndTheRightArm()
    {
        var floor = FurnitureFloor.For(Table());

        AssertThat(floor.Rooms).ContainsExactly(
            FurnitureFloor.Storefront, FurnitureFloor.BackRoom, FurnitureFloor.RightArm);
        AssertThat(FurnitureFloor.Storefront).IsEqual(new FurnitureRect(0.5f, 0.5f, 15.5f, 11.5f));
    }

    [TestCase]
    public static void OwnedLotWidensOnlyTheStorefrontMinimumX()
    {
        var floor = FurnitureFloor.For(Table("lot_a"));

        AssertThat(floor.Rooms[0]).IsEqual(FurnitureFloor.Storefront with { MinX = -7.5f });
        AssertThat(floor.Rooms[1]).IsEqual(FurnitureFloor.BackRoom);
        AssertThat(floor.Rooms[2]).IsEqual(FurnitureFloor.RightArm);
    }

    [TestCase]
    public static void TwoOwnedLotsTakeTheSmallestStorefrontEdge()
    {
        var floor = FurnitureFloor.For(Table("lot_a", "lot_b"));

        AssertThat(floor.Rooms[0].MinX).IsEqual(-11.5f);
        AssertThat(floor.Rooms[0].MaxX).IsEqual(15.5f);
    }

    [TestCase]
    public static void APointInsideAnOwnedLotIsValidOnlyAfterItIsOwned()
    {
        var pose = new FurniturePose(-4f, 6f, 0);

        AssertThat(FurniturePlacementModel.IsValid(Shelf, pose, None, FurnitureFloor.For(Table()))).IsFalse();
        AssertThat(FurniturePlacementModel.IsValid(Shelf, pose, None, FurnitureFloor.For(Table("lot_a")))).IsTrue();
    }

    [TestCase]
    public static void TheStorageRoomsTakeFurniture()
    {
        // The starting storage rack stands in the right storage arm, and the back room holds the backoffice.
        AssertThat(FurniturePlacementModel.IsValid(Shelf, new FurniturePose(20f, -6.5f, 0), None, FurnitureFloor.Shop))
            .IsTrue();
        AssertThat(FurniturePlacementModel.IsValid(Shelf, new FurniturePose(4f, -4f, 0), None, FurnitureFloor.Shop))
            .IsTrue();
        AssertThat(FurniturePlacementModel.IsValid(Shelf, new FurniturePose(20f, 6f, 1), None, FurnitureFloor.Shop))
            .IsTrue();
    }

    [TestCase]
    public static void APieceAcrossTheWallBetweenTwoRoomsIsRefused()
    {
        // z 0 is the wall between the storefront and the back room, and x 16 the wall to the right arm.
        AssertThat(FurniturePlacementModel.IsValid(Shelf, new FurniturePose(8f, 0f, 0), None, FurnitureFloor.Shop))
            .IsFalse();
        AssertThat(FurniturePlacementModel.IsValid(Shelf, new FurniturePose(16f, 6f, 0), None, FurnitureFloor.Shop))
            .IsFalse();
    }
}
