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
    private const float Near = 1e-4f;

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
    public void ProductShelfHasEightSlotsAndJoinsTheSalesGroupOnly()
    {
        AssertThat(_shelf.Kind).IsEqual(ShelfKind.ProductShelf);
        AssertThat(_shelf.SlotCount).IsEqual(8);
        AssertBool(_shelf.IsInGroup(CardShelf.SalesGroupName)).IsTrue();
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
    public async Task FourBoostersStackInOneSlotAndAFifthIsEjected()
    {
        var slot = _shelf.Slots.First();
        for (var i = 0; i < ShelfPlacementRule.BoostersPerProductSlot; i++)
            await Drop(slot, MakeContainer(BoosterScene));
        var extra = MakeContainer(BoosterScene);
        extra.Freeze = false;

        await Drop(slot, extra);

        AssertThat(slot.Cards.Count).IsEqual(4);
        for (var i = 0; i < slot.Cards.Count; i++)
            AssertVector(slot.Cards[i].GlobalPosition).IsEqualApprox(slot.SeatPosition(i), new Vector3(Near, Near, Near));
        AssertBool(extra.Freeze).IsFalse();
        AssertBool(slot.Cards.Contains(extra)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ASlotHoldingAPackTakesNothingMore()
    {
        var slot = _shelf.Slots.First();
        await Drop(slot, MakeContainer(PackScene));
        var booster = MakeContainer(BoosterScene);
        booster.Freeze = false;

        await Drop(slot, booster);

        AssertBool(slot.HasRoom).IsFalse();
        AssertBool(slot.Cards.Contains(booster)).IsFalse();
        AssertBool(booster.Freeze).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task APackIsEjectedFromASlotHoldingBoosters()
    {
        var slot = _shelf.Slots.First();
        await Drop(slot, MakeContainer(BoosterScene));
        var pack = MakeContainer(PackScene);
        pack.Freeze = false;

        await Drop(slot, pack);

        AssertBool(slot.HasRoom).IsTrue();
        AssertBool(slot.CanTake(StockKind.Pack)).IsFalse();
        AssertBool(slot.CanTake(StockKind.Booster)).IsTrue();
        AssertBool(slot.Cards.Contains(pack)).IsFalse();
        AssertBool(pack.Freeze).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AFullProductShelfHoldsEightPacksOrThirtyTwoBoosters()
    {
        AssertThat(_shelf.Slots.Sum(slot => slot.SeatsFor(StockKind.Pack))).IsEqual(8);
        AssertThat(_shelf.Slots.Sum(slot => slot.SeatsFor(StockKind.Booster))).IsEqual(32);
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
