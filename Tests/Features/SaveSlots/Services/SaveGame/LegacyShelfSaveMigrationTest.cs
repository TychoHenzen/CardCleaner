using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.SaveSlots.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.Features.Packs;
using CardCleaner.Tests.Scenes;
using CardCleaner.Tests.TestUtilities;
using Godot;
using Saveable;
using Saveable.Extensions;

namespace CardCleaner.Tests.Features.SaveSlots.Services.SaveGame;

/// <summary>
/// A save made before the shop's starting product shelf and storage rack became furniture kept their stock at the old
/// seats, and its price tags saved markups under the old node paths. Loaded into the current shop, its stock goes back
/// onto the same board (whose seats then fill in order) and the old markups are ignored, so the stock asks its market price. A current save loads as
/// it is.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LegacyShelfSaveMigrationTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong BoosterSeed = 4242;
    private const int OldMarkup = 20;
    private const int SettleFrames = 10;
    private const float SameSeatMeters = 0.05f;

    // Before the move the product shelf stood at (10, 0, 10.5) under World/Storefront, its second slot sat at
    // (0, 0.9, -0.2) on it, and a slot seated stock 0.16 m up and 0.2 m forward of itself.
    private static readonly Vector3 OldSecondSlotSeat = new(10f, 1.06f, 10.5f);

    private static readonly (string Current, string Old)[] ShelfPaths =
    [
        ("/World/Furniture/ProductShelf/Content/", "/World/Storefront/ProductShelf/"),
        ("/World/Furniture/StorageRack/Content/", "/World/RightStorage/StorageRack/"),
    ];

    private SceneTree _tree = null!;
    private Node? _previousScene;
    private Node3D _shop = null!;
    private GameSaveService _saves = null!;
    private CardShelf _product = null!;
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
    public async Task AnOldSaveKeepsItsShelfStockAndDropsItsOldMarkup()
    {
        var slot = (DeckSlot)_product.FindChild("Slot2", true, false);
        var pricing = _product.PricingFor(slot)!;
        AssertBool(slot.TryLock(NewBooster())).IsTrue();
        AssertBool(_saves.ManualSave()).IsTrue();
        WriteAsOldSave(slot.SeatPosition(0), pricing);

        _saves.LoadGame();
        await SettleAsync();

        AssertThat(pricing.AskingValue).IsEqual(ProductMarketPrices.BoosterPrice);
        // The old save had it in the board's second seat with the first empty; the board closes up the gap.
        var booster = _product.LevelOf(slot)!.Stock.OfType<CardContainer>().Single();
        AssertThat(booster.ContentSeed).IsEqual(BoosterSeed);
        AssertThat(BoostersWithTheSeed()).IsEqual(1);
    }

    [TestCase]
    public void ACurrentSaveIsLoadedAsItIs()
    {
        AssertBool(((DeckSlot)_product.FindChild("Slot2", true, false)).TryLock(NewBooster())).IsTrue();
        AssertBool(_saves.ManualSave()).IsTrue();
        var payload = ReadPayload();

        AssertThat(LegacyShelfSaveMigration.Migrate(payload, _shop)).IsEqual(payload);
    }

    // Rewrites the slot's save the way the old layout wrote it: a markup for `tag` under the tag's old path, the stock
    // seated at `currentSeat` back at the old seat, and no furniture entry, which the old layout never saved.
    private void WriteAsOldSave(Vector3 currentSeat, SlotPricing tag)
    {
        var save = SaveExtension.DeserializeObject<TreeSave>(ReadPayload())!;
        var tagPath = tag.GetPath().ToString();
        var oldTagPath = ShelfPaths.Aggregate(tagPath, (path, paths) => path.Replace(paths.Current, paths.Old));
        AssertThat(oldTagPath).IsNotEqual(tagPath);
        var markup = new NodeSave();
        markup.SetOrAddProperty("markup", OldMarkup);
        save.SetOrAddCollection($"slot_price:{oldTagPath}", markup);

        save.Collections.Remove("furniture");
        var game = save.Collections["game_save_service"];
        AssertBool(game.TryGetProperty<List<object>>("containers", out var containers)).IsTrue();
        var moved = 0;
        game.SetOrAddProperty("containers", SavedContainers.Relocate(containers!, position =>
        {
            if (position.DistanceTo(currentSeat) >= SameSeatMeters)
                return null;

            moved++;
            return OldSecondSlotSeat;
        }));
        AssertThat(moved).IsEqual(1);

        AssertBool(new SlotStore(_files).Save(_slot, SaveExtension.SerializeObject(save), DateTime.UtcNow)).IsTrue();
    }

    private string ReadPayload() => new SlotStore(_files).Load(_slot).Envelope!.Payload;

    private CardContainer NewBooster()
    {
        var container = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        container.ContentSeed = BoosterSeed;
        container.Spawning = new RecordingCardSpawner();
        _shop.GetNode<Node3D>("World").AddChild(container);
        return container;
    }

    private int BoostersWithTheSeed() =>
        _tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Count(container => container.ContentSeed == BoosterSeed);

    private static async Task SettleAsync()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
