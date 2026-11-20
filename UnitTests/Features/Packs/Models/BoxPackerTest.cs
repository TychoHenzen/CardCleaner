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
    public static void TheCartonSpaceIsTheInsideOfTheCartonMesh()
    {
        var wall = ContainerMeshRecipe.CartonWall;

        AssertThat(CartonSpace.Interior.X).IsEqualApprox(ContainerMeshRecipe.CartonWidth - 2f * wall, Tolerance);
        AssertThat(CartonSpace.Interior.Z).IsEqualApprox(ContainerMeshRecipe.CartonDepth - 2f * wall, Tolerance);
        AssertThat(CartonSpace.Floor).IsEqualApprox(wall, Tolerance);
        // From the inside floor up to the underside of the closed flaps.
        AssertThat(CartonSpace.Floor + CartonSpace.Interior.Y)
            .IsEqualApprox(ContainerMeshRecipe.CartonHeight - 2f * wall, Tolerance);
        AssertThat(CartonSpace.OutsideHeight).IsEqualApprox(ContainerMeshRecipe.CartonHeight, Tolerance);
    }

    [TestCase]
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
    public static void ACartonHoldsEightBoosterBoxesAndNoMore()
    {
        var placed = PackAll(CartonSpace.Packer(), Enumerable.Repeat(ItemShape.Pack, 8));

        AssertThat(placed.Count).IsEqual(8);
        AssertThat(CartonSpace.Packer().Place(placed, ItemShape.Pack) == null).IsTrue();
        AssertSound(CartonSpace.Interior, placed);
    }

    [TestCase]
    public static void ARunOfCardsFormsOneStack()
    {
        var placed = PackAll(CartonSpace.Packer(), Enumerable.Repeat(ItemShape.Card, 10));

        AssertThat(placed.Select(item => new Vector2(item.Min.X, item.Min.Z)).Distinct().Count()).IsEqual(1);
        for (var i = 0; i < placed.Count; i++)
            AssertThat(placed[i].Min.Y).IsEqualApprox(i * ItemShapes.Size(ItemShape.Card).Y, Tolerance);
    }

    [TestCase]
    public static void AFullStackOfCardsStartsANewStackOnTheFloor()
    {
        var placed = PackAll(CartonSpace.Packer(), Enumerable.Repeat(ItemShape.Card, 200));
        var firstStack = placed.TakeWhile(item => SameSpot(item, placed[0])).Count();

        AssertThat(firstStack >= 128).OverrideFailureMessage($"the first stack holds {firstStack} cards").IsTrue();
        AssertThat(placed[firstStack].Min.Y).IsEqual(0f);
        AssertThat(placed.Skip(firstStack).All(item => SameSpot(item, placed[firstStack]))).IsTrue();
    }

    [TestCase]
    public static void AnEmptyBoxTakesAtLeast512CardsOfTheirRealSize()
    {
        var placed = PackAll(CartonSpace.Packer(), Enumerable.Repeat(ItemShape.Card, 600));
        var card = new Vector3(CardMeshBakeTest.Width, CardMeshBakeTest.Thickness, CardMeshBakeTest.Height);

        AssertThat(placed.Count >= 512).OverrideFailureMessage($"the box took {placed.Count} cards").IsTrue();
        AssertThat(CartonSpace.Packer().Place(placed, ItemShape.Card) == null).IsTrue();
        AssertThat(placed.All(item =>
                ItemShapes.SizeIn(card, item.Pose) + new Vector3(2f * BoxPacker.Clearance, 0f, 2f * BoxPacker.Clearance)
                    == item.Size))
            .IsTrue();
        AssertSound(CartonSpace.Interior, placed);
    }

    [TestCase]
    public static void CardsFillTheFourPinwheelStacksOneAtATime()
    {
        var packer = CartonSpace.Packer();
        var placed = PackAll(packer, Enumerable.Repeat(ItemShape.Card, 600));

        var spots = placed.Select(item => new Vector2(item.Min.X, item.Min.Z)).Distinct().ToList();
        AssertThat(spots.SequenceEqual(packer.CardStacks.Select(stack => new Vector2(stack.Floor.X, stack.Floor.Z))))
            .OverrideFailureMessage($"cards lie at {string.Join(", ", spots)}").IsTrue();
        // Each stack is one unbroken run of the order the cards went in.
        var runs = placed.Select((item, i) => i == 0 || !SameSpot(item, placed[i - 1])).Count(starts => starts);
        AssertThat(runs).IsEqual(4);
    }

    [TestCase]
    public static void TheLastCardInIsTheFirstOut()
    {
        var cards = Enumerable.Range(0, 200).Select(price => (StoredItem)new StoredCard(null, null, price));
        var (box, left) = BoxContents.Packed(CartonSpace.Packer(), cards);
        var last = new StoredCard(null, null, 999);

        var withLast = box.With(last)!;
        var spot = withLast.Placed[^1];
        var (taken, spilled) = withLast.Without(withLast.Count - 1);

        AssertThat(left.Count).IsEqual(0);
        AssertThat(withLast.Items[withLast.LastIndexOf(item => item is StoredCard)]).IsEqual(last);
        // Nothing lies over it: it is the top of its stack.
        AssertThat(withLast.Placed.Take(withLast.Count - 1)
                .Any(item => item.Min.Y >= spot.Max.Y - Tolerance && FloorShare(item, spot) > 0f))
            .IsFalse();
        AssertThat(spilled.Count).IsEqual(0);
        AssertThat(taken.Items.SequenceEqual(box.Items) && taken.Placed.SequenceEqual(box.Placed)).IsTrue();
    }

    [TestCase]
    public static void CardsPutAfterBoosterBoxesUseTheStackPlacesTheyLeaveFree()
    {
        var shapes = Enumerable.Repeat(ItemShape.Pack, 2).Concat(Enumerable.Repeat(ItemShape.Card, 600)).ToList();

        var placed = PackAll(CartonSpace.Packer(), shapes);

        AssertThat(placed.Count(item => item.Shape == ItemShape.Pack)).IsEqual(2);
        AssertThat(placed.Count(item => item.Shape == ItemShape.Card) is > 0 and < 512).IsTrue();
        AssertSound(CartonSpace.Interior, placed);
    }

    [TestCase]
    public static void AMixedBoxNeverOverlapsLeavesTheBoxOrRestsOnAir()
    {
        var random = new Random(7);
        var shapes = Enumerable.Range(0, 400).Select(_ => (ItemShape)random.Next(3)).ToList();

        var placed = PackAll(CartonSpace.Packer(), shapes);

        // Cards lie only on the four stack places, never where the corner search puts containers, so a random mix
        // packs few cards; every shape still goes in.
        AssertThat(placed.Select(item => item.Shape).Distinct().Count()).OverrideFailureMessage("placed " + string.Join(
                ", ", placed.GroupBy(item => item.Shape).Select(group => $"{group.Count()} {group.Key}")))
            .IsEqual(3);
        AssertSound(CartonSpace.Interior, placed);
    }

    // The two orders a player meets: cards put into a delivered box after half its booster boxes came out, and cards
    // put in after one booster pack. The counts are printed, so a change in what a box holds shows in the test log.
    [TestCase(4, ItemShape.Pack)]
    [TestCase(1, ItemShape.Booster)]
    public static void CardsStillGoInAfterContainers(int containers, ItemShape shape)
    {
        var shapes = Enumerable.Repeat(shape, containers).Concat(Enumerable.Repeat(ItemShape.Card, 600)).ToList();

        var placed = PackAll(CartonSpace.Packer(), shapes);
        var cards = placed.Count(item => item.Shape == ItemShape.Card);

        Console.WriteLine($"box packer: {containers} {shape} then cards: {cards} cards fit");
        AssertThat(placed.Count(item => item.Shape == shape)).IsEqual(containers);
        AssertThat(cards >= 128).OverrideFailureMessage($"only {cards} cards fit").IsTrue();
        AssertSound(CartonSpace.Interior, placed);
    }

    [TestCase]
    public static void TheSameItemsInTheSameOrderLandInTheSamePlaces()
    {
        ItemShape[] shapes = [ItemShape.Pack, ItemShape.Card, ItemShape.Card, ItemShape.Booster, ItemShape.Pack];

        var first = PackAll(CartonSpace.Packer(), shapes);
        var second = PackAll(CartonSpace.Packer(), shapes);

        AssertThat(first.SequenceEqual(second)).IsTrue();
    }

    [TestCase]
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

    private static bool SameSpot(PackedItem a, PackedItem b) =>
        Math.Abs(a.Min.X - b.Min.X) <= Tolerance && Math.Abs(a.Min.Z - b.Min.Z) <= Tolerance;

    // Every item inside the box, none overlapping another, none resting on air. The problems are gathered and asserted
    // once, since a full box has hundreds of thousands of pairs.
    private static void AssertSound(Vector3 interior, IReadOnlyList<PackedItem> placed)
    {
        var problems = new List<string>();
        for (var i = 0; i < placed.Count; i++)
        {
            var item = placed[i];
            if (item.Min.X < -Tolerance || item.Min.Y < -Tolerance || item.Min.Z < -Tolerance
                || item.Max.X > interior.X + Tolerance || item.Max.Y > interior.Y + Tolerance
                || item.Max.Z > interior.Z + Tolerance)
                problems.Add($"item {i} lies outside the box");

            var carried = 0f;
            for (var j = 0; j < placed.Count; j++)
            {
                if (j == i)
                    continue;
                if (Overlap(item, placed[j]))
                    problems.Add($"items {i} and {j} overlap");
                if (Math.Abs(placed[j].Max.Y - item.Min.Y) <= Tolerance)
                    carried += FloorShare(item, placed[j]);
            }

            if (item.Min.Y > Tolerance && carried < BoxPacker.SupportShare * item.Size.X * item.Size.Z)
                problems.Add($"item {i} rests on air");
        }

        AssertThat(string.Join("; ", problems.Take(5))).IsEqual("");
    }

    private static bool Overlap(PackedItem a, PackedItem b) =>
        a.Min.X < b.Max.X - Tolerance && a.Max.X > b.Min.X + Tolerance
                                      && a.Min.Y < b.Max.Y - Tolerance && a.Max.Y > b.Min.Y + Tolerance
                                      && a.Min.Z < b.Max.Z - Tolerance && a.Max.Z > b.Min.Z + Tolerance;

    private static float FloorShare(PackedItem a, PackedItem b) =>
        Math.Max(0f, Math.Min(a.Max.X, b.Max.X) - Math.Max(a.Min.X, b.Min.X))
        * Math.Max(0f, Math.Min(a.Max.Z, b.Max.Z) - Math.Max(a.Min.Z, b.Min.Z));
}
