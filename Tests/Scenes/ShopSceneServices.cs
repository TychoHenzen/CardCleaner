using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Hands the services an instantiated shop scene owns to the test container, because a test scene is not the
/// current scene and the composition root does not pick them up.
/// </summary>
public static class ShopSceneServices
{
    public static void Register(Node3D shop, ulong seed)
    {
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IGameSettings>(shop.GetNode<GameSettings>("Services/GameSettings"));
        TestServices.Container.RegisterSingleton<IMoneyService>(shop.GetNode<MoneyService>("Services/MoneyService"));
        TestServices.Container.RegisterSingleton<IOrderingService>(
            shop.GetNode<OrderingService>("Services/OrderingService"));
        TestServices.Container.RegisterSingleton(RandomSources.Create(seed));
        TestServices.Container.RegisterSingleton(Substitute.For<ICardGenerator>());
        TestServices.Container.RegisterSingleton<ICardSpawningService>(
            shop.GetNode<CardSpawningService>("Services/SpawningService"));
    }

    public static void RegisterWithCardSpawner(Node3D shop, ulong seed)
    {
        Register(shop, seed);
        TestServices.Container.RegisterSingleton<ICardSpawner>((ICardSpawner)shop.GetNode("World/Cards"));
    }
}
