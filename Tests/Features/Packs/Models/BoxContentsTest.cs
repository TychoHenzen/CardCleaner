using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Packs.Models;

[TestSuite]
public class BoxContentsTest
{
    private static readonly StoredContainer BoosterBox =
        new("res://Scenes/Shop/Items/Pack.tscn", CardContainerKind.Pack, 11UL, null);

    private static readonly StoredContainer Booster =
        new("res://Scenes/Shop/Items/Booster.tscn", CardContainerKind.Booster, 12UL, 40);

    [TestCase]
    [TestCategory("Unit")]
    public static void TakingTheLastItemLeavesTheRestWhereTheyLay()
    {
        var contents = Contents(BoosterBox, Card(1), Booster);
        var before = contents.Placed.Take(2).ToList();
        var spilled = new List<StoredItem>();

        var taken = contents.TakeAt(contents.Count - 1, spilled);

        AssertThat(taken).IsEqual(Booster);
        AssertThat(spilled.Count).IsEqual(0);
        AssertThat(contents.Placed.SequenceEqual(before)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TakingAnEarlierItemPacksTheLaterOnesAgainInTheirOrder()
    {
        var contents = Contents(BoosterBox, Card(1), Card(2));
        var spilled = new List<StoredItem>();

        contents.TakeAt(0, spilled);

        AssertThat(contents.Items.ToArray()).ContainsExactly(Card(1), Card(2));
        AssertThat(contents.Placed[0].Min.Y).IsEqual(0f);
        AssertThat(spilled.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AFullBoxRefusesAnItemAndKeepsWhatItHolds()
    {
        var contents = Contents(Enumerable.Repeat<StoredItem>(BoosterBox, 8).ToArray());

        AssertThat(contents.HasRoomFor(ItemShape.Pack)).IsFalse();
        AssertThat(contents.TryAdd(BoosterBox)).IsFalse();
        AssertThat(contents.Count).IsEqual(8);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RefillingReturnsWhatDoesNotFit()
    {
        var contents = new BoxContents(CartonSpace.Packer());

        var spilled = contents.Refill(Enumerable.Repeat<StoredItem>(BoosterBox, 9));

        AssertThat(contents.Count).IsEqual(8);
        AssertThat(spilled.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheLastItemASlotTakesIsFoundFromTheTop()
    {
        var contents = Contents(Card(1), BoosterBox, Card(2), Booster);

        AssertThat(contents.LastIndexOf(item => item is StoredCard)).IsEqual(2);
        AssertThat(contents.LastIndexOf(item => item is StoredContainer { Kind: CardContainerKind.Pack }))
            .IsEqual(1);
        AssertThat(contents.LastIndexOf(_ => false)).IsEqual(-1);
    }

    private static StoredCard Card(int seed) => new(null, seed, null);

    private static BoxContents Contents(params StoredItem[] items)
    {
        var contents = new BoxContents(CartonSpace.Packer());
        foreach (var item in items)
            AssertThat(contents.TryAdd(item)).IsTrue();
        return contents;
    }
}
