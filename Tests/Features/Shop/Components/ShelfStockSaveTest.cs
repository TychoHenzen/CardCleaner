using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.Scenes;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Tests.TestUtilities.Helpers;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
/// Stock on the shop's product shelf and storage rack goes into the shop save and comes back in the same slots after a
/// reload. The restored stock is locked back into its slot during the load, before any physics step, each board keeps
/// its stock in the order it was put on with its prices, and a container that lies outside every slot restores without
/// an error. The slot files live in an in-memory file system.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShelfStockSaveTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const ulong BoosterSeed = 4242;
    private const ulong BoxSeed = 99;
    private const ulong LooseBoxSeed = 55;
    private const int SettleFrames = 10;
    private static readonly Vector3 NoSlotPosition = new(50f, 5f, 50f);

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private GameSaveService _saves = null!;
    private CardShelf _product = null!;
    private CardShelf _rack = null!;
    private InMemorySlotFileSystem _files = null!;
    private SaveSlotName _slot;

    [BeforeTest]
    public async Task Setup()
    {
        _tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = _tree.CurrentScene;

        // The rig resets the container, so the save services are registered after it and before the shop enters
        // the tree; the shop's GameSaveService resolves them in its _Ready.
        _shop = ShopOrderingRig.LoadShopWithServices();

        _files = new InMemorySlotFileSystem();
        var slots = new ActiveSaveSlot();
        if (!SaveSlotName.TryCreate("a", out _slot, out var reason))
            throw new InvalidOperationException(reason);
        slots.Activate(_slot);

        _saves = _shop.GetNode<GameSaveService>("Services/GameSaveService");
        TestServices.Container.RegisterSingleton<IActiveSaveSlot>(slots);
        TestServices.Container.RegisterSingleton<ISlotFileSystem>(_files);
        TestServices.Container.RegisterSingleton<SlotStore>(new SlotStore(_files));
        TestServices.Container.RegisterSingleton<ISaveGameCommand>(_saves);

        _product = _shop.GetNode<CardShelf>("World/Furniture/ProductShelf/Content");
        _rack = _shop.GetNode<CardShelf>("World/Furniture/StorageRack/Content");

        AddNode(_shop, autoFree: false);
        _tree.CurrentScene = _shop;
        await SettleAsync();
    }

    [AfterTest]
    public void Teardown()
    {
        _tree.CurrentScene = _previousScene;
        _shop.Free();
        ShopPauseRig.Teardown();
    }

    [TestCase]
    public async Task LockedBoosterAndBoxReLockIntoTheirSlotsAfterReload()
    {
        var before = await LockStockAndReloadAsync();

        var productSlot = _product.Slots.First();
        var rackSlot = _rack.Slots.First();
        AssertBool(productSlot.HasCards).IsTrue();
        AssertBool(rackSlot.HasCards).IsTrue();

        var booster = productSlot.Cards.OfType<CardContainer>().Single();
        AssertThat(booster.Kind).IsEqual(CardContainerKind.Booster);
        AssertThat(booster.ContentSeed).IsEqual(BoosterSeed);

        var box = rackSlot.Cards.OfType<CardContainer>().Single();
        AssertThat(box.Kind).IsEqual(CardContainerKind.Box);
        AssertThat(box.ContentSeed).IsEqual(BoxSeed);

        AssertThat(_product.StockedCount).IsEqual(1);
        AssertThat(_rack.StockedCount).IsEqual(1);
        AssertThat(UnopenedContainers()).IsEqual(before);
    }

    // A seat of the product shelf lies inside the shelf's own solid collider, which pushes a free container off the
    // seat, and out of the slot's trigger, on the first physics step. So the load itself must lock the stock.
    [TestCase]
    public async Task StockIsBackInItsSlotsBeforeAnyPhysicsStep()
    {
        AssertBool(_product.Slots.First().TryLock(NewContainer(BoosterScene, BoosterSeed))).IsTrue();
        AssertBool(_rack.Slots.First().TryLock(NewContainer(BoxScene, BoxSeed))).IsTrue();
        AssertBool(_saves.ManualSave()).IsTrue();

        _saves.LoadGame();

        AssertThat(_product.Slots.First().Cards.OfType<CardContainer>().Single().ContentSeed).IsEqual(BoosterSeed);
        AssertThat(_rack.Slots.First().Cards.OfType<CardContainer>().Single().ContentSeed).IsEqual(BoxSeed);
        await BoundedWait.ForQueuedFrees();
    }

    // Five boosters on a product board (four on its first seat, one on its second) all ask the first one's price, and
    // two cards on a card board keep their own; after a reload, before any physics step, each board holds the same
    // items in the same order at the same prices.
    [TestCase]
    public async Task EachBoardsStockComesBackInOrderWithItsPricesBeforeAnyPhysicsStep()
    {
        var boosters = _product.Levels.First();
        for (var i = 0; i < 5; i++)
        {
            var booster = NewContainer(BoosterScene, BoosterSeed + (ulong)i);
            booster.ShopPrice = i == 0 ? 7 : 20;
            AssertBool(boosters.TryLock(booster)).IsTrue();
        }

        var cards = _shop.GetNode<CardShelf>("World/Furniture/CardShelf/Content").Levels.First();
        foreach (var (febris, price) in new[] { (0.25f, 3), (0.5f, 12) })
        {
            var card = ShopTestCards.Create(new CardSignature([0f, febris, 0f, 0f, 0f, 0f, 0f, 0f]));
            card.ShopPrice = price;
            _shop.GetNode<Node3D>("World").AddChild(card);
            AssertBool(cards.TryLock(card)).IsTrue();
        }

        var savedBoosters = Boosters(boosters);
        var savedCards = Cards(cards);
        AssertThat(savedBoosters.Select(b => b.Price).Distinct().Single()).IsEqual(7);
        AssertThat(boosters.Seats[0].Cards.Count).IsEqual(4);
        AssertBool(_saves.ManualSave()).IsTrue();

        _saves.LoadGame();

        AssertThat(Boosters(boosters)).IsEqual(savedBoosters);
        AssertThat(boosters.Seats[0].Cards.Count).IsEqual(4);
        AssertThat(boosters.Seats[1].Cards.Count).IsEqual(1);
        AssertThat(Cards(cards)).IsEqual(savedCards);
        AssertThat(Cards(cards).Select(c => c.Price).ToArray()).IsEqual(new int?[] { 3, 12 });
        await BoundedWait.ForQueuedFrees();
    }

    [TestCase]
    public async Task ABoxOnAProductShelfSlotIsIgnoredAndStaysUnlockedAfterReload()
    {
        var slot2 = (DeckSlot)_product.FindChild("Slot2", true, false);
        var box = NewContainer(BoxScene, LooseBoxSeed);
        box.GlobalPosition = slot2.GlobalPosition;

        AssertBool(_saves.ManualSave()).IsTrue();
        _saves.LoadGame();
        await SettleAsync();

        AssertBool(slot2.HasCards).IsFalse();
        var restored = WithSeed(LooseBoxSeed);
        AssertThat(restored.Length).IsEqual(1);
        AssertBool(restored[0].Freeze).IsFalse();
    }

    [TestCase]
    public async Task ASealedContainerOutsideEverySlotRestoresWithoutAnErrorAndStockStillRelocks()
    {
        var booster = NewContainer(BoosterScene, BoosterSeed);
        AssertBool(_product.Slots.First().TryLock(booster)).IsTrue();
        var loose = NewContainer(BoxScene, LooseBoxSeed);
        loose.GlobalPosition = NoSlotPosition;
        AssertBool(_saves.ManualSave()).IsTrue();

        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            _saves.LoadGame();
            await SettleAsync();
        }
        finally
        {
            ILog.Sink = previous;
        }

        AssertThat(log.Errors.Count).IsEqual(0);
        AssertBool(_product.Slots.First().HasCards).IsTrue();
        var restored = WithSeed(LooseBoxSeed);
        AssertThat(restored.Length).IsEqual(1);
        AssertBool(restored[0].GetParent() is DeckSlot).IsFalse();
    }

    // Locks a booster into the product shelf's first slot and a box into the storage rack's first slot, saves, loads
    // and lets physics settle. Returns the unopened container count taken before the save.
    private async Task<int> LockStockAndReloadAsync()
    {
        var booster = NewContainer(BoosterScene, BoosterSeed);
        AssertBool(_product.Slots.First().TryLock(booster)).IsTrue();
        var box = NewContainer(BoxScene, BoxSeed);
        AssertBool(_rack.Slots.First().TryLock(box)).IsTrue();

        var before = UnopenedContainers();
        AssertBool(_saves.ManualSave()).IsTrue();
        _saves.LoadGame();
        await SettleAsync();
        return before;
    }

    // The boosters on a level in the order they were put on, by seed and price.
    private static (ulong? Seed, int? Price)[] Boosters(ShelfLevel level) =>
        level.Stock.Cast<CardContainer>().Select(b => (b.ContentSeed, b.ShopPrice)).ToArray();

    // The cards on a level in the order they were put on, by their second signature element and price.
    private static (float Febris, int? Price)[] Cards(ShelfLevel level) =>
        level.Stock.Cast<CardController>().Select(c => (c.Signature.Elements[1], c.ShopPrice)).ToArray();

    private CardContainer NewContainer(string scenePath, ulong seed)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.ContentSeed = seed;
        container.Spawning = new RecordingCardSpawner();
        _shop.GetNode<Node3D>("World").AddChild(container);
        return container;
    }

    private int UnopenedContainers() =>
        _tree.GetNodesInGroup("card_containers").OfType<CardContainer>().Count(container => !container.IsOpened);

    private CardContainer[] WithSeed(ulong seed) =>
        _tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(container => container.ContentSeed == seed)
            .ToArray();

    private static Task SettleAsync() => BoundedWait.SyncPhysicsFrames(SettleFrames);

    private sealed class RecordingLog : ILog
    {
        public System.Collections.Generic.List<string> Errors { get; } = new();

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogError(string message) => Errors.Add(message);

        public void LogError(string message, Exception exception) => Errors.Add(message);
    }
}
