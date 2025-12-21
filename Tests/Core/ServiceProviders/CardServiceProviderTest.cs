using CardCleaner.Scripts.Core.ServiceProviders;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Core.ServiceProviders;

[TestSuite]
[RequireGodotRuntime]
public class CardServiceProviderTest
{
    private CardServiceProvider _provider = null!;
    private ServiceContainer _container = null!;

    [BeforeTest]
    public void Setup()
    {
        _provider = new CardServiceProvider
        {
            RarityVisuals = null!,
            BaseCardTypes = null!,
            GemVisuals = null!,
            CardRoot = null!,
        };
        _container = new ServiceContainer();

        // Set up required exports with minimal test data
        _provider.RarityVisuals = CreateTestRarityVisuals();
        _provider.BaseCardTypes = CreateTestBaseCardTypes();
        _provider.GemVisuals = CreateTestGemVisuals();

        Assertions.AddNode(_provider);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RegisterServices_WithValidData_RegistersAllServices()
    {
        // Act
        _provider.RegisterServices(_container);

        // Assert - All required services should be registered
        Assertions.AssertBool(_container.IsRegistered<RarityVisual[]>()).IsTrue();
        Assertions.AssertBool(_container.IsRegistered<BaseCardType[]>()).IsTrue();
        Assertions.AssertBool(_container.IsRegistered<GemVisual[]>()).IsTrue();
        Assertions.AssertBool(_container.IsRegistered<ICardGenerator>()).IsTrue();
        Assertions.AssertBool(_container.IsRegistered<ICardSpawner>()).IsTrue();
        // Note: ICardSpawningService is registered by CardSpawningService itself, not CardServiceProvider
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Ready_AddsToServiceProvidersGroup()
    {
        // Act
        _provider._Ready();

        // Assert
        Assertions.AssertBool(_provider.IsInGroup("service_providers")).IsTrue();
    }

    private static RarityVisual[] CreateTestRarityVisuals()
    {
        var rarity = new RarityVisual
        {
            Rarity = CardRarity.Common
        };
        return new[] { rarity };
    }

    private static BaseCardType[] CreateTestBaseCardTypes()
    {
        var baseType = new BaseCardType
        {
            TypeName = "Test Card"
        };
        return new[] { baseType };
    }

    private static GemVisual[] CreateTestGemVisuals()
    {
        var gem = new GemVisual
        {
            Element = Element.Solidum
        };
        return new[] { gem };
    }
}