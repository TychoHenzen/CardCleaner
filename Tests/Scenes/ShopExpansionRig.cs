using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     One live shop scene wired for the expansion suites: slot files in memory (shared between rigs, so a second rig
///     loads what the first saved), the services handed to the test container, and the nodes the cases read.
/// </summary>
public sealed class ShopExpansionRig
{
    public const string LotId = "side_wing_1";

    /// <summary>Enough for the first lot (1500) on top of the starting balance.</summary>
    public const int LotFunds = 1500;

    /// <summary>The checkout payment that lifts the shop to the level the first lot needs (level 2, 50 XP).</summary>
    public const int PaymentForLevelTwo = 500;

    public static readonly Vector3 ShelfArea = new(2f, 0f, 7f);
    public static readonly Vector3 LotStorefrontPart = new(-4f, 0f, 6f);
    public static readonly Vector3 StorageInside = new(4f, 0f, -4f);
    public static readonly Vector3 LotStoragePart = new(-4f, 0f, -4f);

    private const string BoxScenePath = "res://Scenes/Shop/Items/ShelfBox.tscn";
    private const int SettleFrames = 10;

    private readonly SceneTree _tree = (SceneTree)Engine.GetMainLoop();
    private Node? _previousScene;
    private bool _closed;

    private ShopExpansionRig(Node3D shop)
    {
        Shop = shop;
        Region = shop.GetNode<ShopNavigationRegion>("World/Navigation");
        Controller = shop.GetNode<FurniturePlacementController>("Services/FurniturePlacement");
        Saves = shop.GetNode<GameSaveService>("Services/GameSaveService");
        Expansion = shop.GetNode<ShopExpansionService>("Services/ShopExpansion");
        Money = shop.GetNode<MoneyService>("Services/MoneyService");
        Spawner = shop.GetNode<CustomerSpawner>("Services/CustomerSpawner");
        View = shop.GetNode<ExpansionLotView>("World/ExpansionLot");
        FurnitureRoot = shop.GetNode<Node3D>("World/Furniture");
    }

    public Node3D Shop { get; }

    public ShopNavigationRegion Region { get; }

    public FurniturePlacementController Controller { get; }

    public GameSaveService Saves { get; }

    public ShopExpansionService Expansion { get; }

    public MoneyService Money { get; }

    public CustomerSpawner Spawner { get; }

    public ExpansionLotView View { get; }

    public Node3D FurnitureRoot { get; }

    /// <summary>Loads the shop on the "expansion" slot of <paramref name="files" />, after its first bake.</summary>
    public static async Task<ShopExpansionRig> StartAsync(InMemorySlotFileSystem files)
    {
        var shop = ShopOrderingRig.LoadShopWithServices();
        var rig = new ShopExpansionRig(shop);
        rig.RegisterSlot(files);
        // Inject before the shop enters the tree, so each node's _Ready finds its resolver.
        TestServices.Inject(rig.Expansion);
        TestServices.Inject(rig.Controller);
        TestServices.Inject(rig.Spawner);
        TestServices.Inject(rig.View);
        // The camera aim is not driven headless; the cases place the pose with AimAt.
        rig.Controller.AimOverride = true;

        uint regionBefore = NavigationServer3D.RegionGetIterationId(rig.Region.GetRid());
        rig._previousScene = rig._tree.CurrentScene;
        rig._tree.Root.AddChild(shop);
        rig._tree.CurrentScene = shop;
        await ShopNavigationRig.WaitForBakeSync(rig.Region, regionBefore);
        return rig;
    }

    public void Close()
    {
        if (_closed)
            return;
        _closed = true;
        Controller.Cancel();
        _tree.CurrentScene = _previousScene;
        Shop.Free();
    }

    /// <summary>Gives the shop the money and the level for the first lot.</summary>
    public void MeetTheLotRequirements()
    {
        Money.Add(LotFunds);
        Shop.GetNode<ShopProgression>("Services/ShopProgression").AwardCheckout(PaymentForLevelTwo);
    }

    /// <summary>Buys the lot and waits for the rebake that follows.</summary>
    public async Task<PurchaseOutcome> BuyAsync(string lotId = LotId)
    {
        uint before = NavigationServer3D.RegionGetIterationId(Region.GetRid());
        var outcome = Expansion.TryBuy(lotId);
        if (outcome == PurchaseOutcome.Bought)
            await ShopNavigationRig.WaitForBakeSync(Region, before);
        return outcome;
    }

    public bool Reaches(Vector3 from, Vector3 to) => Region.Reaches(from, to);

    /// <summary>Places a shelf through begin, aim and confirm, and waits for the rebake.</summary>
    public async Task<PlacedFurniture> PlaceShelfAsync(float x, float z)
    {
        Controller.Begin(NewBox());
        Controller.AimAt(x, z);
        uint before = NavigationServer3D.RegionGetIterationId(Region.GetRid());
        if (!Controller.Confirm())
            throw new InvalidOperationException($"the shelf at ({x}, {z}) was not placed");
        await ShopNavigationRig.WaitForBakeSync(Region, before);
        await SettleAsync();
        return (PlacedFurniture)FurnitureRoot.GetChild(FurnitureRoot.GetChildCount() - 1);
    }

    public FurnitureBox NewBox()
    {
        var box = GD.Load<PackedScene>(BoxScenePath).Instantiate<FurnitureBox>();
        TestServices.Inject(box);
        Shop.GetNode("World").AddChild(box);
        return box;
    }

    public static async Task SettleAsync()
    {
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }

    private void RegisterSlot(InMemorySlotFileSystem files)
    {
        var slots = new ActiveSaveSlot();
        if (!SaveSlotName.TryCreate("expansion", out var slot, out var reason))
            throw new InvalidOperationException(reason);
        slots.Activate(slot);
        TestServices.Container.RegisterSingleton<IActiveSaveSlot>(slots);
        TestServices.Container.RegisterSingleton<ISlotFileSystem>(files);
        TestServices.Container.RegisterSingleton<SlotStore>(new SlotStore(files));
        TestServices.Container.RegisterSingleton<ISaveGameCommand>(Saves);
    }
}
