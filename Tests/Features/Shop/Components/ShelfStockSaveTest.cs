using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.Scenes;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
/// Stock on the shop's product shelf and storage rack goes into the shop save and comes back in the same slots after a
/// reload. The restored stock re-locks through the slot's own body entry, is never sold at the register, and a container
/// that lies outside every slot restores without an error. The slot files live in an in-memory file system.
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
    private SaleRegister _register = null!;
    private MoneyService _money = null!;
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

        _product = _shop.GetNode<CardShelf>("World/Storefront/ProductShelf");
        _rack = _shop.GetNode<CardShelf>("World/RightStorage/StorageRack");
        _register = _shop.GetNode<SaleRegister>("World/Storefront/CheckoutCounter");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");

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
    [TestCategory("Unit")]
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

    [TestCase]
    [TestCategory("Unit")]
    public async Task ABoxOnAProductShelfSlotIsIgnoredAndStaysUnlockedAfterReload()
    {
        var slot2 = _product.GetNode<DeckSlot>("Slot2");
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
    [TestCategory("Unit")]
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

    [TestCase]
    [TestCategory("Unit")]
    public async Task RestoredStockIsNeverSoldAtTheRegister()
    {
        await LockStockAndReloadAsync();
        var balance = _money.Balance;

        var result = _register.Sell();

        AssertThat(result.Status).IsEqual(SaleStatus.NothingToSell);
        AssertThat(_money.Balance).IsEqual(balance);
        AssertBool(_product.Slots.First().HasCards).IsTrue();
        AssertBool(_rack.Slots.First().HasCards).IsTrue();
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

    private static async Task SettleAsync()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }

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
