using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Loads the shop scene for the ordering suites and hands its scene-owned
/// services to the test service container, since a test scene is not the
/// current scene and its autoload-style registration never runs.
/// </summary>
public static class ShopOrderingRig
{
    private const string UnusedOptionsPath =
        "user://test_shop_ordering_rig_unused.cfg";

    public static Node3D LoadShopWithServices()
    {
        var shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath)
            .Instantiate<Node3D>();

        TestServices.Reset();
        RegisterOptions();
        RegisterShopServices(shop);
        return shop;
    }

    // Reset empties the container, and the shop's pause and options menus
    // resolve OptionsStore when they ready. A missing one logs an exception
    // from _Ready that later crashes Godot's finalizer (exit 0xC000001D), so
    // register one on a file that is never saved to.
    private static void RegisterOptions()
    {
        var container = TestServices.Container;
        container.RegisterSingleton<OptionsStore>(
            new OptionsStore(UnusedOptionsPath));
        container.RegisterSingleton<OptionsApplier>(new OptionsApplier());
    }

    private static void RegisterShopServices(Node3D shop)
    {
        var container = TestServices.Container;
        container.RegisterSingleton<IGameSettings>(
            Service<GameSettings>(shop, "GameSettings"));
        container.RegisterSingleton<IMoneyService>(
            Service<MoneyService>(shop, "MoneyService"));
        container.RegisterSingleton<IShopProgression>(
            Service<ShopProgression>(shop, "ShopProgression"));
        container.RegisterSingleton<IShopExpansion>(
            Service<ShopExpansionService>(shop, "ShopExpansion"));
        container.RegisterSingleton<ICursorOwner>(
            Service<CursorOwner>(shop, "CursorOwner"));
        container.RegisterSingleton<IOrderingService>(
            Service<OrderingService>(shop, "OrderingService"));
        container.RegisterSingleton<IStaffService>(
            Service<StaffService>(shop, "Staff"));
        // No autoload injects the shop (it is not the current scene);
        // the furniture load reads the lots through it.
        TestServices.Inject(
            Service<FurnitureSaveService>(shop, "FurnitureSave"));
        TestServices.Inject(Service<StaffService>(shop, "Staff"));
    }

    private static T Service<T>(Node shop, string name) where T : Node =>
        shop.GetNode<T>($"Services/{name}");
}
