using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Shelves;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Shelves;

/// <summary>
///     One shelf board as a store, in a bare world: it holds one kind of stock at a time and takes a new kind once
///     emptied, fills its seats in order whichever seat an item is aimed at, gives back the last item put on, closes up
///     the gap a body taken from the middle leaves, and prices by its shelf: every item on a product-shelf level asks the
///     first item's price, while each card on a card-shelf level keeps its own.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShelfLevelTest
{
    private const string ProductShelfScene = "res://Scenes/Shop/Items/ProductShelf.tscn";
    private const string CardShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";

    private Node3D _world = null!;

    [BeforeTest]
    public async Task Setup()
    {
        TestServices.Reset();
        // A level builds what it takes through the registered card spawner, as the shop registers its own.
        TestServices.Container.RegisterSingleton<ICardSpawningService>(new ShopTestCardSpawner());
        _world = AddNode(new Node3D());
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static async Task Teardown()
    {
        await BoundedWait.ForQueuedFrees();
        TestServices.Reset();
    }

    [TestCase]
    public void ALevelHoldsOneKindOfStockAndAnotherLevelTakesTheOther()
    {
        var levels = Shelf(ProductShelfScene).Levels.ToArray();

        AssertBool(levels[0].Put(Booster(1))).IsTrue();

        AssertBool(levels[0].CanPut(BoosterBox(2))).IsFalse();
        AssertBool(levels[0].Put(BoosterBox(2))).IsFalse();
        AssertThat(levels[0].Count).IsEqual(1);
        AssertBool(levels[1].Put(BoosterBox(2))).IsTrue();
        AssertThat(levels[0].Holds).IsEqual(StockKind.Booster);
        AssertThat(levels[1].Holds).IsEqual(StockKind.Pack);
    }

    [TestCase]
    public void AnEmptiedLevelTakesANewKind()
    {
        var level = Shelf(ProductShelfScene).Levels.First();
        AssertBool(level.Put(Booster(1))).IsTrue();

        AssertObject(level.TakeLast()).IsNotNull();

        AssertObject(level.Holds).IsNull();
        AssertBool(level.Put(BoosterBox(2))).IsTrue();
        AssertThat(level.Holds).IsEqual(StockKind.Pack);
    }

    // Four boosters stack on a seat, so the fifth starts the next seat.
    [TestCase]
    public void ALevelFillsItsSeatsInOrder()
    {
        var level = Shelf(ProductShelfScene).Levels.First();

        for (var i = 0; i < ShelfPlacementRule.BoostersPerProductSlot + 1; i++)
            AssertBool(level.Put(Booster((ulong)i + 1))).IsTrue();

        AssertThat(level.Seats[0].Cards.Count).IsEqual(ShelfPlacementRule.BoostersPerProductSlot);
        AssertThat(level.Seats[1].Cards.Count).IsEqual(1);
        AssertBool(level.Seats.Skip(2).Any(seat => seat.HasCards)).IsFalse();
        AssertThat(level.EmptySeats).IsEqual(level.Seats.Count - 2);
    }

    [TestCase]
    public void ATakeGivesBackTheLastItemPutOn()
    {
        var level = Shelf(ProductShelfScene).Levels.First();
        AssertBool(level.Put(Booster(11))).IsTrue();
        AssertBool(level.Put(Booster(22))).IsTrue();

        var taken = level.TakeLast();

        AssertBool(taken is StoredContainer { Seed: 22 }).IsTrue();
        AssertThat(level.Count).IsEqual(1);
        AssertBool(((CardContainer)level.Last!).ContentSeed == 11).IsTrue();
    }

    // The first booster on an empty level sets its price; every later one asks that price, whatever it had.
    [TestCase]
    public void EveryItemOnAProductShelfLevelAsksTheFirstItemsPrice()
    {
        var level = Shelf(ProductShelfScene).Levels.First();

        AssertBool(level.Put(Booster(1, price: 5))).IsTrue();
        AssertBool(level.Put(Booster(2, price: 9))).IsTrue();
        AssertBool(level.Put(Booster(3))).IsTrue();

        AssertThat(level.Stock.Cast<CardContainer>().Select(booster => booster.ShopPrice).ToArray())
            .IsEqual(new int?[] { 5, 5, 5 });
    }

    [TestCase]
    public void TwoCardsOnOneCardShelfLevelKeepTheirOwnPrices()
    {
        var level = Shelf(CardShelfScene).Levels.First();

        AssertBool(level.Put(Card(0.25f, price: 3))).IsTrue();
        AssertBool(level.Put(Card(0.5f, price: 12))).IsTrue();

        var cards = level.Stock.Cast<CardController>().ToArray();
        AssertThat(cards.Length).IsEqual(2);
        AssertThat(cards[0].ShopPrice).IsEqual(3);
        AssertThat(cards[1].ShopPrice).IsEqual(12);
        AssertBool(level.Seats[0].Cards.Single() == cards[0] && level.Seats[1].Cards.Single() == cards[1]).IsTrue();
    }

    [TestCase]
    public void ACardShelfLevelRefusesABoosterAndAProductShelfLevelRefusesACard()
    {
        var cardLevel = Shelf(CardShelfScene).Levels.First();
        var productLevel = Shelf(ProductShelfScene, new Vector3(3f, 0f, -5f)).Levels.First();

        AssertBool(cardLevel.Put(Booster(1))).IsFalse();
        AssertBool(productLevel.Put(Card(0.25f))).IsFalse();
        AssertThat(cardLevel.Count + productLevel.Count).IsEqual(0);
    }

    // A card taken by hand from the middle of a level leaves a gap; the later card moves down into it, so the seats
    // stay filled in order and the last card put on is still the one a take gives back.
    [TestCase]
    public async Task ALevelClosesUpTheGapABodyTakenFromTheMiddleLeaves()
    {
        var level = Shelf(CardShelfScene).Levels.First();
        AssertBool(level.Put(Card(0.25f))).IsTrue();
        AssertBool(level.Put(Card(0.5f))).IsTrue();
        AssertBool(level.Put(Card(0.75f))).IsTrue();
        var middle = level.Seats[1].Cards.Single();
        var last = level.Seats[2].Cards.Single();

        // Away from the board at once, as a hand takes it, so no trigger reports it landing on the level again.
        middle.Reparent(_world);
        middle.GlobalPosition = new Vector3(30f, 5f, 30f);
        await ISceneRunner.SyncProcessFrame;

        AssertThat(level.Count).IsEqual(2);
        AssertBool(level.Seats[1].Cards.SingleOrDefault() == last).IsTrue();
        AssertBool(level.Seats[2].HasCards).IsFalse();
        AssertBool(level.Last == last).IsTrue();
    }

    // Away from the origin, where loose bodies appear, so no trigger catches one on its own.
    private CardShelf Shelf(string scene, Vector3? at = null)
    {
        var shelf = GD.Load<PackedScene>(scene).Instantiate<CardShelf>();
        shelf.Position = at ?? new Vector3(0f, 0f, -5f);
        _world.AddChild(shelf);
        return shelf;
    }

    private static StoredContainer Booster(ulong seed, int? price = null) =>
        new(BoosterScene, CardContainerKind.Booster, seed, price);

    private static StoredContainer BoosterBox(ulong seed) => new(PackScene, CardContainerKind.Pack, seed, null);

    private static StoredCard Card(float febris, int? price = null) =>
        new(new CardSignature([0f, febris, 0f, 0f, 0f, 0f, 0f, 0f]), 7, price);
}
