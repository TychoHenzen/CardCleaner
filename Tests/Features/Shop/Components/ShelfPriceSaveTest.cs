using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.Scenes;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;
using Saveable;
using Saveable.Extensions;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
/// Prices survive a save and reload of the shop: every booster, pack and card keeps its own price, which comes back with
/// it into its shelf slot. A tag markup from an older save is ignored, and a corrupt container price falls back to the
/// market price without stopping the load. The slot files live in an in-memory file system.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShelfPriceSaveTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong BoosterSeed = 4242;
    private const int SettleFrames = 10;

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private GameSaveService _saves = null!;
    private CardShelf _product = null!;
    private CardShelf _cards = null!;
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

        _product = _shop.GetNode<CardShelf>("World/Furniture/ProductShelf/Content");
        _cards = _shop.GetNode<CardShelf>("World/Furniture/CardShelf/Content");
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
    public async Task TheBoosterPriceSurvivesSaveAndReload()
    {
        var slot1 = _product.GetNode<DeckSlot>("Slot1");
        var booster = NewBooster();
        AssertBool(slot1.TryLock(booster)).IsTrue();
        var pricing = _product.PricingFor(slot1)!;
        pricing.PricedStock()!.SetPrice(6);

        AssertBool(_saves.ManualSave()).IsTrue();
        booster.ShopPrice = null;
        _saves.LoadGame();
        await SettleAsync();

        var restored = slot1.Cards.OfType<CardContainer>().Single();
        AssertBool(restored.ShopPrice == 6).IsTrue();
        AssertThat(pricing.AskingValue).IsEqual(6);
        AssertThat(pricing.TagLabel!.Text).IsEqual("6");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardPriceComesBackWithTheCardInItsSlot()
    {
        var slot1 = _cards.GetNode<DeckSlot>("Slot1");
        var card = ShopTestCards.Create(new CardSignature());
        _shop.GetNode<Node3D>("World").AddChild(card);
        AssertBool(slot1.TryLock(card)).IsTrue();
        _cards.PricingFor(slot1)!.PricedStock()!.SetPrice(9);

        AssertBool(_saves.ManualSave()).IsTrue();
        _saves.LoadGame();
        await SettleAsync();

        var restored = slot1.Cards.OfType<CardController>().Single();
        AssertBool(restored.ShopPrice == 9).IsTrue();
        AssertThat(_cards.PricingFor(slot1)!.TagLabel!.Text).IsEqual("9");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnOldTagMarkupIsIgnoredOnLoad()
    {
        var slot1 = _product.GetNode<DeckSlot>("Slot1");
        AssertBool(slot1.TryLock(NewBooster())).IsTrue();
        var pricing = _product.PricingFor(slot1)!;

        // Before prices lived with the stock, each tag saved a markup percentage under its own node path.
        var oldTag = new NodeSave();
        oldTag.SetOrAddProperty("markup", 50);
        var money = new NodeSave();
        money.SetOrAddProperty("balance", 321);
        var tree = new TreeSave();
        tree.SetOrAddCollection($"slot_price:{pricing.GetPath()}", oldTag);
        tree.SetOrAddCollection(_money.UniqueID.ToString(), money);

        SaveSystem.LoadTreeFromJson(SaveExtension.SerializeObject(tree), _shop);

        AssertThat(pricing.AskingValue).IsEqual(ProductMarketPrices.BoosterPrice);
        AssertThat(_money.Balance).IsEqual(321);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ACorruptContainerPriceReadsAsTheMarketPrice()
    {
        var raw = new List<object> { BoosterRecord("abc"), BoosterRecord(-3L), BoosterRecord(6L) };

        var records = SavedContainers.Parse(raw);

        AssertBool(records[0].Price.HasValue).IsFalse();
        AssertBool(records[1].Price.HasValue).IsFalse();
        AssertBool(records[2].Price == 6).IsTrue();
    }

    private static Dictionary<string, object> BoosterRecord(object price) => new()
    {
        ["ScenePath"] = BoosterScene,
        ["Kind"] = nameof(CardContainerKind.Booster),
        ["Position"] = Vector3.Zero,
        ["Rotation"] = Vector3.Zero,
        ["Price"] = price,
    };

    private CardContainer NewBooster()
    {
        var container = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        container.ContentSeed = BoosterSeed;
        container.Spawning = new RecordingCardSpawner();
        _shop.GetNode<Node3D>("World").AddChild(container);
        return container;
    }

    private static async Task SettleAsync()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
