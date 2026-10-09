using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardShelfTest
{
    private const string ShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const float FloorThickness = 0.2f;
    private const int SettleFrames = 30;
    private const int StabilityFrames = 90;
    private const float StabilityTolerance = 0.01f;

    private CardShelf _shelf = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var floor = new StaticBody3D { Position = new Vector3(0f, -FloorThickness / 2f, 0f) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10f, FloorThickness, 10f) } });
        AddNode(floor);
        _shelf = GD.Load<PackedScene>(ShelfScene).Instantiate<CardShelf>();
        AddNode(_shelf);
        await Frames(SettleFrames);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ShelfSceneHasSeveralOneCardSlotsAndJoinsTheShelfGroup()
    {
        AssertThat(_shelf.SlotCount).IsGreater(1);
        AssertBool(_shelf.Slots.All(slot => slot.Capacity == 1)).IsTrue();
        AssertBool(_shelf.IsInGroup(CardShelf.GroupName)).IsTrue();
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

        AssertBool(_shelf.Release(first)).IsTrue();
        var second = MakeCard();
        await Drop(slot, second);

        AssertThat(_shelf.StockedCount).IsEqual(1);
        AssertBool(_shelf.StockedCards.Contains(second)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ReleasingACardThatIsNotOnTheShelfIsRefused()
    {
        AssertBool(_shelf.Release(MakeCard())).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ARigidBodyThatOnlyHasCardInItsNameIsNotLockedIntoASlot()
    {
        var box = new RigidBody3D { Name = "CardboardBox" };
        AddNode(box);

        _shelf.Slots.First().Area.EmitSignal(Area3D.SignalName.BodyEntered, box);
        await Frames(2);

        AssertThat(_shelf.StockedCount).IsEqual(0);
        AssertBool(box.Freeze).IsFalse();
        AssertThat(box.GetParent()).IsNotEqual(_shelf.Slots.First());
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

    [TestCase]
    [TestCategory("Unit")]
    public async Task StockedCardsAndTheShelfStayStillWhilePhysicsRuns()
    {
        var slots = _shelf.Slots.ToArray();
        var cards = new CardController[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            cards[i] = MakeCard();
            await Drop(slots[i], cards[i]);
        }

        var shelfBefore = _shelf.GlobalPosition;
        var cardsBefore = cards.Select(c => c.GlobalPosition).ToArray();

        await Frames(StabilityFrames);

        AssertThat(_shelf.StockedCount).IsEqual(slots.Length);
        AssertBool(_shelf.GlobalPosition.DistanceTo(shelfBefore) < StabilityTolerance).IsTrue();
        for (var i = 0; i < cards.Length; i++)
        {
            AssertBool(cards[i].Freeze).IsTrue();
            AssertBool(cards[i].GlobalPosition.DistanceTo(cardsBefore[i]) < StabilityTolerance).IsTrue();
        }
    }

    private static CardController MakeCard()
    {
        var card = ShopTestCards.Create(new CardSignature());
        AddNode(card);
        return card;
    }

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }

    private static async Task Drop(DeckSlot slot, RigidBody3D card)
    {
        slot.Area.EmitSignal(Area3D.SignalName.BodyEntered, card);
        await Frames(2);
    }
}
