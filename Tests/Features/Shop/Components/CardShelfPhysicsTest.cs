using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardShelfPhysicsTest : CardShelfTestBase
{
    private const int StabilityFrames = 90;
    private const float StabilityTolerance = 0.01f;

    [TestCase]
    [TestCategory("Unit")]
    public async Task ARigidBodyThatOnlyHasCardInItsNameIsNotLockedIntoASlot()
    {
        var box = new RigidBody3D { Name = "CardboardBox" };
        AddNode(box);

        var slot = _shelf.Slots.First();
        slot.Area.EmitSignal(Area3D.SignalName.BodyEntered, box);
        await Frames(2);

        AssertThat(_shelf.StockedCount).IsEqual(0);
        AssertBool(box.Freeze).IsFalse();
        AssertThat(box.GetParent()).IsNotEqual(_shelf.Slots.First());
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
        var shelfMoved = _shelf.GlobalPosition.DistanceTo(shelfBefore);
        AssertBool(shelfMoved < StabilityTolerance).IsTrue();
        for (var i = 0; i < cards.Length; i++)
        {
            var cardMoved = cards[i].GlobalPosition.DistanceTo(cardsBefore[i]);
            AssertBool(cards[i].Freeze).IsTrue();
            AssertBool(cardMoved < StabilityTolerance).IsTrue();
        }
    }
}
