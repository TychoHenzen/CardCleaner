using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Hand.Models;

/// <summary>
///     How many of each record a hand takes: a card or booster pack one place, a box or booster box an empty hand.
/// </summary>
[TestSuite]
public class HandRoomTest
{
    private static readonly StoredItem Card = new StoredCard(null, 1, null);

    private static readonly StoredItem Booster =
        new StoredContainer("res://Scenes/Shop/Items/Booster.tscn", CardContainerKind.Booster, 1, null);

    private static readonly StoredItem BoosterBox =
        new StoredContainer("res://Scenes/Shop/Items/Pack.tscn", CardContainerKind.Pack, 1, null);

    private static readonly StoredItem CardboardBox =
        new StoredBox("res://Scenes/Shop/Items/CardboardBox.tscn", null, null, false);

    [TestCase]
    public static void ACardOrABoosterTakesOnePlaceWhileThereIsRoom()
    {
        AssertThat(HandRoom.For(Card, 3, 97)).IsEqual(97);
        AssertThat(HandRoom.For(Booster, 3, 97)).IsEqual(97);
        AssertThat(HandRoom.For(Card, 100, 0)).IsEqual(0);
    }

    [TestCase]
    public static void ABoosterBoxOrACardboardBoxGoesOnlyIntoAnEmptyHand()
    {
        foreach (var exclusive in new[] { BoosterBox, CardboardBox })
        {
            AssertThat(HandRoom.For(exclusive, 0, 100)).IsEqual(1);
            AssertThat(HandRoom.For(exclusive, 1, 99)).IsEqual(0);
        }
    }
}
