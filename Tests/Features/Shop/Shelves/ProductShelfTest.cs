using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using Godot;
using CardCleaner.Scripts.Features.Shop.Shelves;

namespace CardCleaner.Tests.Features.Shop.Shelves;

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
        // Away from the origin, where the containers are made, so none lands in a bottom-board slot's trigger on its own.
        _shelf.Position = new Vector3(0, 0, -5);
        AddNode(_shelf);
        await Frames(SettleFrames);
    }

    [TestCase]
    public void ProductShelfHasSixteenSlotsAndJoinsTheSalesGroupOnly()
    {
        // Four slots on each of its four boards.
        AssertThat(_shelf.Kind).IsEqual(ShelfKind.ProductShelf);
        AssertThat(_shelf.SlotCount).IsEqual(16);
        AssertBool(_shelf.IsInGroup(CardShelf.SalesGroupName)).IsTrue();
        AssertBool(_shelf.IsInGroup(CardShelf.StorageGroupName)).IsFalse();
    }

    // A body landing anywhere on a board goes into that level's first free seat, whichever seat's trigger it entered.
    [TestCase]
    public async Task ABoosterAndABoosterBoxDroppedOnTwoLevelsLockIntoTheirFirstSeats()
    {
        var levels = _shelf.Levels.ToArray();
        var booster = MakeContainer(BoosterScene);
        var pack = MakeContainer(PackScene);

        await Drop(levels[0].Seats[^1], booster);
        await Drop(levels[1].Seats[^1], pack);

        AssertBool(booster.Freeze).IsTrue();
        AssertThat(booster.GetParent()).IsEqual(levels[0].Seats[0]);
        AssertBool(pack.Freeze).IsTrue();
        AssertThat(pack.GetParent()).IsEqual(levels[1].Seats[0]);
        AssertThat(_shelf.StockedCount).IsEqual(2);
    }

    [TestCase]
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
    public async Task ReparentingABoosterAwayFreesItsSlot()
    {
        var slot = _shelf.Slots.First();
        var booster = MakeContainer(BoosterScene);
        await Drop(slot, booster);

        booster.Reparent(AddNode(new Node3D()));

        AssertBool(slot.HasCards).IsFalse();
        AssertThat(_shelf.StockedCount).IsEqual(0);
    }

    // Boosters stack four to a seat, the seats filling in order, and a booster dropped on a full level is pushed off.
    [TestCase]
    public async Task BoostersStackFourToASeatAndAFullLevelPushesTheNextOneOff()
    {
        var level = _shelf.Levels.First();
        var seat = level.Seats[0];
        var perLevel = level.Seats.Count * ShelfPlacementRule.BoostersPerProductSlot;
        for (var i = 0; i < perLevel; i++)
            await Drop(seat, MakeContainer(BoosterScene));
        var extra = MakeContainer(BoosterScene);
        extra.Freeze = false;

        await Drop(seat, extra);

        foreach (var each in level.Seats)
        {
            AssertThat(each.Cards.Count).IsEqual(ShelfPlacementRule.BoostersPerProductSlot);
            for (var i = 0; i < each.Cards.Count; i++)
                AssertVector(each.Cards[i].GlobalPosition)
                    .IsEqualApprox(each.SeatPosition(i), new Vector3(Near, Near, Near));
        }

        AssertBool(extra.Freeze).IsFalse();
        AssertBool(level.Stock.Contains(extra)).IsFalse();
    }

    [TestCase]
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
    public void AFullProductShelfHoldsSixteenPacksOrSixtyFourBoosters()
    {
        AssertThat(_shelf.Slots.Sum(slot => slot.SeatsFor(StockKind.Pack))).IsEqual(16);
        AssertThat(_shelf.Slots.Sum(slot => slot.SeatsFor(StockKind.Booster))).IsEqual(64);
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
