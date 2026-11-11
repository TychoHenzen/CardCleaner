using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

// Shared setup for the CardShelf suites: a floor, one shelf settled on it, and the drop and wait helpers.
public abstract class CardShelfTestBase
{
    private const string ShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const float FloorThickness = 0.2f;
    private const int SettleFrames = 30;

    protected CardShelf _shelf = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var floor = new StaticBody3D
        {
            Position = new Vector3(0f, -FloorThickness / 2f, 0f),
        };
        var floorShape = new BoxShape3D
        {
            Size = new Vector3(10f, FloorThickness, 10f),
        };
        floor.AddChild(new CollisionShape3D { Shape = floorShape });
        AddNode(floor);
        _shelf = GD.Load<PackedScene>(ShelfScene).Instantiate<CardShelf>();
        AddNode(_shelf);
        await Frames(SettleFrames);
    }

    protected static CardController MakeCard()
    {
        var card = ShopTestCards.Create(new CardSignature());
        AddNode(card);
        return card;
    }

    protected static async Task Frames(int count)
    {
        var message = await BoundedWait.PhysicsFrames(count);
        AssertThat(message).IsNull();
    }

    protected static async Task Drop(DeckSlot slot, RigidBody3D card)
    {
        slot.Area.EmitSignal(Area3D.SignalName.BodyEntered, card);
        await Frames(2);
    }
}
