using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.Features.Card.Components;
using CardCleaner.Tests.Features.Packs.Components;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
public class BoxPackerTest
{
    private const float Tolerance = 1e-4f;

    [TestCase]
    [TestCategory("Unit")]
    public static void TheCartonSpaceIsTheInsideOfTheCartonMesh()
    {
        var wall = ContainerMeshRecipe.CartonWall;

        AssertThat(CartonSpace.Interior.X).IsEqualApprox(ContainerMeshRecipe.CartonWidth - 2f * wall, Tolerance);
        AssertThat(CartonSpace.Interior.Z).IsEqualApprox(ContainerMeshRecipe.CartonDepth - 2f * wall, Tolerance);
        AssertThat(CartonSpace.Floor).IsEqualApprox(wall, Tolerance);
        // From the inside floor up to the underside of the closed flaps.
        AssertThat(CartonSpace.Floor + CartonSpace.Interior.Y)
            .IsEqualApprox(ContainerMeshRecipe.CartonHeight - 2f * wall, Tolerance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EachItemIsPackedAtTheSizeOfItsMesh()
    {
        AssertThat(ItemShapes.Size(ItemShape.Card)).IsEqual(new Vector3(
            CardMeshBakeTest.Width, CardMeshBakeTest.Thickness, CardMeshBakeTest.Height));
        AssertThat(ItemShapes.Size(ItemShape.Booster)).IsEqual(new Vector3(ContainerMeshRecipe.BoosterWidth,
            ContainerMeshRecipe.BoosterThickness, ContainerMeshRecipe.BoosterLength));
        AssertThat(ItemShapes.Size(ItemShape.Pack)).IsEqual(new Vector3(
            ContainerMeshRecipe.PackWidth, ContainerMeshRecipe.PackHeight, ContainerMeshRecipe.PackDepth));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ACartonHoldsEightBoosterBoxesAndNoMore()
    {
        var placed = PackAll(CartonSpace.Packer(), Enumerable.Repeat(ItemShape.Pack, 8));

        AssertThat(placed.Count).IsEqual(8);
        AssertThat(CartonSpace.Packer().Place(placed, ItemShape.Pack) == null).IsTrue();
        AssertSound(CartonSpace.Interior, placed);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ARunOfCardsFormsOneStack()
    {
        var placed = PackAll(CartonSpace.Packer(), Enumerable.Repeat(ItemShape.Card, 10));

        AssertThat(placed.Select(item => new Vector2(item.Min.X, item.Min.Z)).Distinct().Count()).IsEqual(1);
        for (var i = 0; i < placed.Count; i++)
            AssertThat(placed[i].Min.Y).IsEqualApprox(i * ItemShapes.Size(ItemShape.Card).Y, Tolerance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AFullStackOfCardsStartsANewStackOnTheFloor()
    {
        var perStack = (int)(CartonSpace.Interior.Y / ItemShapes.Size(ItemShape.Card).Y + Tolerance);
        var placed = PackAll(CartonSpace.Packer(), Enumerable.Repeat(ItemShape.Card, perStack + 1));

        AssertThat(placed.Count).IsEqual(perStack + 1);
        AssertThat(placed[^1].Min.Y).IsEqual(0f);
        AssertThat(placed[^1].Min.X > placed[0].Min.X || placed[^1].Min.Z > placed[0].Min.Z).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AMixedBoxNeverOverlapsLeavesTheBoxOrRestsOnAir()
    {
        var random = new Random(7);
        var shapes = Enumerable.Range(0, 400).Select(_ => (ItemShape)random.Next(3)).ToList();

        var placed = PackAll(CartonSpace.Packer(), shapes);

        AssertThat(placed.Count > 20).IsTrue();
        AssertSound(CartonSpace.Interior, placed);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheSameItemsInTheSameOrderLandInTheSamePlaces()
    {
        ItemShape[] shapes = [ItemShape.Pack, ItemShape.Card, ItemShape.Card, ItemShape.Booster, ItemShape.Pack];

        var first = PackAll(CartonSpace.Packer(), shapes);
        var second = PackAll(CartonSpace.Packer(), shapes);

        AssertThat(first.SequenceEqual(second)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnItemTooBigForTheBoxFitsNowhere()
    {
        var packer = new BoxPacker(new Vector3(0.3f, 0.3f, 0.3f));

        AssertThat(packer.Place([], ItemShape.Card) == null).IsTrue();
    }

    // Puts the shapes in one at a time, skipping any that fit nowhere, as a box does when it refuses an item.
    private static List<PackedItem> PackAll(BoxPacker packer, IEnumerable<ItemShape> shapes)
    {
        var placed = new List<PackedItem>();
        foreach (var shape in shapes)
        {
            if (packer.Place(placed, shape) is { } spot)
                placed.Add(spot);
        }

        return placed;
    }

    private static void AssertSound(Vector3 interior, IReadOnlyList<PackedItem> placed)
    {
        for (var i = 0; i < placed.Count; i++)
        {
            var item = placed[i];
            AssertThat(item.Min.X >= -Tolerance && item.Min.Y >= -Tolerance && item.Min.Z >= -Tolerance).IsTrue();
            AssertThat(item.Max.X <= interior.X + Tolerance && item.Max.Y <= interior.Y + Tolerance
                                                             && item.Max.Z <= interior.Z + Tolerance).IsTrue();
            var carried = 0f;
            for (var j = 0; j < placed.Count; j++)
            {
                if (j == i)
                    continue;
                AssertThat(Overlap(item, placed[j])).OverrideFailureMessage($"items {i} and {j} overlap").IsFalse();
                if (Math.Abs(placed[j].Max.Y - item.Min.Y) <= Tolerance)
                    carried += FloorShare(item, placed[j]);
            }

            var supported = item.Min.Y <= Tolerance || carried >= BoxPacker.SupportShare * item.Size.X * item.Size.Z;
            AssertThat(supported).OverrideFailureMessage($"item {i} rests on air").IsTrue();
        }
    }

    private static bool Overlap(PackedItem a, PackedItem b) =>
        a.Min.X < b.Max.X - Tolerance && a.Max.X > b.Min.X + Tolerance
                                      && a.Min.Y < b.Max.Y - Tolerance && a.Max.Y > b.Min.Y + Tolerance
                                      && a.Min.Z < b.Max.Z - Tolerance && a.Max.Z > b.Min.Z + Tolerance;

    private static float FloorShare(PackedItem a, PackedItem b) =>
        Math.Max(0f, Math.Min(a.Max.X, b.Max.X) - Math.Max(a.Min.X, b.Min.X))
        * Math.Max(0f, Math.Min(a.Max.Z, b.Max.Z) - Math.Max(a.Min.Z, b.Min.Z));
}
