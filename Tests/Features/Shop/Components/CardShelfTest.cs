using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardShelfTest : CardShelfTestBase
{
    [TestCase]
    [TestCategory("Unit")]
    public void ShelfSceneHasSeveralOneCardSlotsAndJoinsTheShelfGroup()
    {
        AssertThat(_shelf.SlotCount).IsGreater(1);
        AssertBool(_shelf.Slots.All(slot => slot.Capacity == 1)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardShelfJoinsTheCardAndSalesGroupsButNotTheStorageGroup()
    {
        AssertThat(_shelf.Kind).IsEqual(ShelfKind.CardShelf);
        AssertBool(_shelf.IsInGroup(CardShelf.SalesGroupName)).IsTrue();
        AssertBool(_shelf.IsInGroup(CardShelf.StorageGroupName)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardDroppedOnASlotIsLockedAndShownOnTheShelf()
    {
        var slot = _shelf.Slots.First();
        var card = MakeCard();

        await Drop(slot, card);

        AssertThat(_shelf.StockedCount).IsEqual(1);
        AssertBool(card.Freeze).IsTrue();
        AssertThat(card.GetParent()).IsEqual(slot);
        AssertBool(_shelf.StockedCards.Contains(card)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PickingTheCardUpTakesItOffTheShelfAgain()
    {
        var slot = _shelf.Slots.First();
        var card = MakeCard();
        await Drop(slot, card);

        card.EmitPickupSignal();

        AssertThat(_shelf.StockedCount).IsEqual(0);
        AssertBool(slot.HasCards).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ReleaseFreesTheSlotForTheNextCard()
    {
        var slot = _shelf.Slots.First();
        var first = MakeCard();
        await Drop(slot, first);

        first.EmitPickupSignal();
        var second = MakeCard();
        await Drop(slot, second);

        AssertThat(_shelf.StockedCount).IsEqual(1);
        AssertBool(_shelf.StockedCards.Contains(second)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EmptySlotConsumesNoSignature()
    {
        AssertThat(_shelf.Slots.First().ConsumeCardSignature()).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardIsLockedWhateverItsNodeIsNamed()
    {
        var slot = _shelf.Slots.First();
        var card = MakeCard();
        card.Name = "DisplayItem";

        await Drop(slot, card);

        AssertThat(_shelf.StockedCount).IsEqual(1);
        AssertBool(card.Freeze).IsTrue();
    }
}
