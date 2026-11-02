using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class StorageRackTest
{
    private const string StorageRackScene = "res://Scenes/Shop/Items/StorageRack.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const int SettleFrames = 2;

    private CardShelf _rack = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rack = GD.Load<PackedScene>(StorageRackScene).Instantiate<CardShelf>();
        AddNode(_rack);
        await Frames(SettleFrames);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StorageRackHasFourSlotsAndJoinsTheStorageGroupOnly()
    {
        AssertThat(_rack.Kind).IsEqual(ShelfKind.StorageRack);
        AssertBool(_rack.IsStorage).IsTrue();
        AssertThat(_rack.SlotCount).IsEqual(4);
        AssertBool(_rack.IsInGroup(CardShelf.StorageGroupName)).IsTrue();
        AssertBool(_rack.IsInGroup(CardShelf.GroupName)).IsFalse();
        AssertBool(_rack.IsInGroup(CardShelf.SalesGroupName)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardboardBoxLocksOntoARackSlot()
    {
        var slot = _rack.Slots.First();
        var box = AddNode(GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>());

        await Drop(slot, box);

        AssertBool(box.Freeze).IsTrue();
        AssertThat(box.GetParent()).IsEqual(slot);
        AssertThat(_rack.StockedCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardIsNotLockedOntoARackSlot()
    {
        var slot = _rack.Slots.ElementAt(1);
        var card = ShopTestCards.Create(new CardSignature());
        AddNode(card);

        await Drop(slot, card);

        AssertBool(card.Freeze).IsFalse();
        AssertThat(card.GetParent()).IsNotEqual(slot);
        AssertThat(_rack.StockedCount).IsEqual(0);
    }

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }

    private static async Task Drop(DeckSlot slot, RigidBody3D body)
    {
        slot.Area.EmitSignal(Area3D.SignalName.BodyEntered, body);
        await Frames(2);
    }
}
