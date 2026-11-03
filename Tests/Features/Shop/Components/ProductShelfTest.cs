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
public class ProductShelfTest
{
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const int SettleFrames = 2;

    private CardShelf _shelf = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shelf = GD.Load<PackedScene>(ProductShelfScene).Instantiate<CardShelf>();
        AddNode(_shelf);
        await Frames(SettleFrames);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ProductShelfHasThreeSlotsAndJoinsTheSalesGroupOnly()
    {
        AssertThat(_shelf.Kind).IsEqual(ShelfKind.ProductShelf);
        AssertThat(_shelf.SlotCount).IsEqual(3);
        AssertBool(_shelf.IsInGroup(CardShelf.SalesGroupName)).IsTrue();
        AssertBool(_shelf.IsInGroup(CardShelf.GroupName)).IsFalse();
        AssertBool(_shelf.IsInGroup(CardShelf.StorageGroupName)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task BoosterAndPackLockOntoTheProductSlots()
    {
        var slots = _shelf.Slots.ToArray();
        var booster = MakeContainer(BoosterScene);
        var pack = MakeContainer(PackScene);

        await Drop(slots[0], booster);
        await Drop(slots[1], pack);

        AssertBool(booster.Freeze).IsTrue();
        AssertThat(booster.GetParent()).IsEqual(slots[0]);
        AssertBool(pack.Freeze).IsTrue();
        AssertThat(pack.GetParent()).IsEqual(slots[1]);
        AssertThat(_shelf.StockedCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardIsNotLockedOntoAProductSlot()
    {
        var slot = _shelf.Slots.ElementAt(2);
        var card = ShopTestCards.Create(new CardSignature());
        AddNode(card);

        await Drop(slot, card);

        AssertBool(card.Freeze).IsFalse();
        AssertThat(card.GetParent()).IsNotEqual(slot);
        AssertThat(_shelf.StockedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ReparentingABoosterAwayFreesItsSlot()
    {
        var slot = _shelf.Slots.First();
        var booster = MakeContainer(BoosterScene);
        await Drop(slot, booster);

        booster.Reparent(AddNode(new Node3D()));

        AssertBool(slot.HasCards).IsFalse();
        AssertThat(_shelf.StockedCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ABoosterDroppedOnAFullProductSlotIsEjected()
    {
        var slot = _shelf.Slots.First();
        var stocked = MakeContainer(BoosterScene);
        await Drop(slot, stocked);
        var extra = MakeContainer(BoosterScene);
        extra.Freeze = false;

        await Drop(slot, extra);

        AssertBool(GodotObject.IsInstanceValid(extra)).IsTrue();
        AssertBool(extra.Freeze).IsFalse();
        AssertBool(slot.Cards.Contains(extra)).IsFalse();
        AssertThat(slot.Cards.Count).IsEqual(1);
        AssertThat(_shelf.StockedCount).IsEqual(1);
    }

    private static CardContainer MakeContainer(string scenePath)
    {
        return AddNode(GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>());
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
