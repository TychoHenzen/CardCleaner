using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Loads the shop scene for the ordering suites and hands its scene-owned services to the service locator,
/// since a test scene is not the current scene and its autoload-style registration never runs.
/// </summary>
public static class ShopOrderingRig
{
    public static Node3D LoadShopWithServices()
    {
        var shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(shop.GetNode<MoneyService>("Services/MoneyService"));
        ServiceLocator.Container.RegisterSingleton<IOrderingService>(
            shop.GetNode<OrderingService>("Services/OrderingService"));
        return shop;
    }
}
