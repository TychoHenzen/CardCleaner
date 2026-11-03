using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
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
/// Each shelf slot's price tag keeps its own markup across a save and reload of the shop. A stocked slot is repainted
/// with its markup when its stock comes back, every tag has its own save id, and one corrupt markup does not stop the
/// rest of the load. The slot files live in an in-memory file system.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShelfPriceSaveTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong BoosterSeed = 4242;
    private const int SettleFrames = 10;
    private const int ExpectedTagCount = 7;

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private GameSaveService _saves = null!;
    private CardShelf _product = null!;
    private CardShelf _rack = null!;
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
    public async Task TheMarkupOfAStockedSlotSurvivesSaveAndReload()
    {
        var slot1 = _product.GetNode<DeckSlot>("Slot1");
        var booster = NewContainer(BoosterScene, BoosterSeed);
        AssertBool(slot1.TryLock(booster)).IsTrue();
        var pricing = _product.PricingFor(slot1)!;
        pricing.SetMarkup(20);

        AssertBool(_saves.ManualSave()).IsTrue();
        pricing.SetMarkup(0);
        _saves.LoadGame();
        await SettleAsync();

        AssertThat(pricing.MarkupPercent).IsEqual(20);
        AssertThat(pricing.AskingValue).IsEqual(AskingPrice.For(ProductMarketPrices.BoosterPrice, 20));
        AssertThat(pricing.TagLabel!.Text).IsEqual(AskingPrice.Readout(ProductMarketPrices.BoosterPrice, 20));
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task EachSlotKeepsItsOwnMarkup()
    {
        var productTags = new[] { 1, 2, 3 }.Select(n => _product.PricingFor(_product.GetNode<DeckSlot>($"Slot{n}"))!)
            .ToArray();
        var rackTag = _rack.PricingFor(_rack.GetNode<DeckSlot>("Slot2"))!;
        var productMarkups = new[] { 10, 20, 30 };
        for (var i = 0; i < productTags.Length; i++)
            productTags[i].SetMarkup(productMarkups[i]);
        rackTag.SetMarkup(40);

        AssertBool(_saves.ManualSave()).IsTrue();
        foreach (var tag in productTags)
            tag.SetMarkup(0);
        rackTag.SetMarkup(0);
        _saves.LoadGame();
        await SettleAsync();

        for (var i = 0; i < productTags.Length; i++)
            AssertThat(productTags[i].MarkupPercent).IsEqual(productMarkups[i]);
        AssertThat(rackTag.MarkupPercent).IsEqual(40);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task AnEmptySlotKeepsItsMarkup()
    {
        var rackTag = _rack.PricingFor(_rack.GetNode<DeckSlot>("Slot3"))!;
        rackTag.SetMarkup(50);

        AssertBool(_saves.ManualSave()).IsTrue();
        rackTag.SetMarkup(0);
        _saves.LoadGame();
        await SettleAsync();

        AssertThat(rackTag.MarkupPercent).IsEqual(50);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryTagInTheShopHasADistinctId()
    {
        var tags = AllPricings(_shop).ToArray();

        AssertThat(tags.Length).IsEqual(ExpectedTagCount);
        AssertThat(tags.Select(tag => tag.UniqueID.ToString()).Distinct().Count()).IsEqual(ExpectedTagCount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ACorruptMarkupDoesNotStopTheRestOfTheLoad()
    {
        var tagA = _product.PricingFor(_product.GetNode<DeckSlot>("Slot1"))!;
        var tagB = _product.PricingFor(_product.GetNode<DeckSlot>("Slot2"))!;
        tagA.SetMarkup(50);
        tagB.SetMarkup(50);

        var corrupt = new NodeSave();
        corrupt.SetOrAddProperty("markup", "abc");
        var good = new NodeSave();
        good.SetOrAddProperty("markup", 30);
        var money = new NodeSave();
        money.SetOrAddProperty("balance", 321);

        var tree = new TreeSave();
        tree.SetOrAddCollection(tagA.UniqueID.ToString(), corrupt);
        tree.SetOrAddCollection(tagB.UniqueID.ToString(), good);
        tree.SetOrAddCollection(_money.UniqueID.ToString(), money);

        SaveSystem.LoadTreeFromJson(SaveExtension.SerializeObject(tree), _shop);

        AssertThat(tagA.MarkupPercent).IsEqual(0);
        AssertThat(tagB.MarkupPercent).IsEqual(30);
        AssertThat(_money.Balance).IsEqual(321);
    }

    private static IEnumerable<SlotPricing> AllPricings(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is SlotPricing pricing)
                yield return pricing;
            foreach (var nested in AllPricings(child))
                yield return nested;
        }
    }

    private CardContainer NewContainer(string scenePath, ulong seed)
    {
        var container = GD.Load<PackedScene>(scenePath).Instantiate<CardContainer>();
        container.ContentSeed = seed;
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
