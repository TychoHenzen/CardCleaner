using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class FurniturePlacementModelTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Snap_RoundsToTheGrid()
    {
        AssertThat(FurniturePlacementModel.Snap(0.26f)).IsEqual(0.5f);
        AssertThat(FurniturePlacementModel.Snap(0.74f)).IsEqual(0.5f);
        AssertThat(FurniturePlacementModel.Snap(0.24f)).IsEqual(0f);
        AssertThat(FurniturePlacementModel.Snap(-0.26f)).IsEqual(-0.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Snap_RoundsHalfStepsAwayFromZero()
    {
        AssertThat(FurniturePlacementModel.Snap(0.25f)).IsEqual(0.5f);
        AssertThat(FurniturePlacementModel.Snap(-0.25f)).IsEqual(-0.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Turn_WrapsInBothDirections()
    {
        AssertThat(FurniturePlacementModel.Turn(3, 1)).IsEqual(0);
        AssertThat(FurniturePlacementModel.Turn(0, -1)).IsEqual(3);
        AssertThat(FurniturePlacementModel.Turn(1, 4)).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RectOf_SwapsWidthAndDepthOnOddTurns()
    {
        var footprint = new FurnitureFootprint(2f, 1f);

        var flat = FurniturePlacementModel.RectOf(footprint, new FurniturePose(4f, 4f, 0));
        AssertThat(flat.MinX).IsEqual(3f);
        AssertThat(flat.MaxX).IsEqual(5f);
        AssertThat(flat.MinZ).IsEqual(3.5f);
        AssertThat(flat.MaxZ).IsEqual(4.5f);

        var turned = FurniturePlacementModel.RectOf(footprint, new FurniturePose(4f, 4f, 1));
        AssertThat(turned.MinX).IsEqual(3.5f);
        AssertThat(turned.MaxX).IsEqual(4.5f);
        AssertThat(turned.MinZ).IsEqual(3f);
        AssertThat(turned.MaxZ).IsEqual(5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IsValid_RejectsOverlapWithPlacedFurniture()
    {
        var placed = new List<FurnitureRect> { new(3f, 3f, 5f, 4f) };

        var valid = FurniturePlacementModel.IsValid(
            new FurnitureFootprint(2f, 1f), new FurniturePose(4f, 3.5f, 0), placed, FurnitureFloor.Shop);

        AssertThat(valid).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IsValid_AllowsTouchingEdges()
    {
        var placed = new List<FurnitureRect> { new(3f, 3f, 5f, 4f) };

        var valid = FurniturePlacementModel.IsValid(
            new FurnitureFootprint(2f, 1f), new FurniturePose(4f, 4.5f, 0), placed, FurnitureFloor.Shop);

        AssertThat(valid).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IsValid_RejectsOutsideTheFloor()
    {
        var footprint = new FurnitureFootprint(2f, 1f);
        var none = new List<FurnitureRect>();

        AssertThat(FurniturePlacementModel.IsValid(
            footprint, new FurniturePose(0.5f, 6f, 0), none, FurnitureFloor.Shop)).IsFalse();
        AssertThat(FurniturePlacementModel.IsValid(
            footprint, new FurniturePose(1.5f, 6f, 0), none, FurnitureFloor.Shop)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IsValid_UsesTheRotatedRectAgainstTheFloor()
    {
        var footprint = new FurnitureFootprint(2f, 1f);
        var none = new List<FurnitureRect>();

        AssertThat(FurniturePlacementModel.IsValid(
            footprint, new FurniturePose(15f, 6f, 1), none, FurnitureFloor.Shop)).IsTrue();
        AssertThat(FurniturePlacementModel.IsValid(
            footprint, new FurniturePose(15f, 6f, 0), none, FurnitureFloor.Shop)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IsValid_RejectsTheWallBand()
    {
        var valid = FurniturePlacementModel.IsValid(
            new FurnitureFootprint(2f, 1f), new FurniturePose(0.5f, 0.5f, 0),
            new List<FurnitureRect>(), FurnitureFloor.Shop);

        AssertThat(valid).IsFalse();
    }
}
