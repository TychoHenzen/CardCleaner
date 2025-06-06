using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.ServiceProviders;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Core.ServiceProviders;

[TestSuite]
[RequireGodotRuntime]
public class GameSettingsProviderTest
{
    private GameSettingsProvider _provider = null!;
    private ServiceContainer _container = null!;

    [BeforeTest]
    public void Setup()
    {
        _provider = new GameSettingsProvider();
        _provider.GameSettings = new GameSettings();
        _container = new ServiceContainer();
        Assertions.AddNode(_provider);
        Assertions.AddNode(_provider.GameSettings);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RegisterServices_WithValidGameSettings_RegistersIGameSettingsService()
    {
        // Arrange
        var gameSettings = CreateTestGameSettings();
        _provider.GameSettings = gameSettings;

        // Act
        _provider.RegisterServices(_container);

        // Assert
        Assertions.AssertBool(_container.IsRegistered<IGameSettings>()).IsTrue();
        
        var resolved = _container.Resolve<IGameSettings>();
        Assertions.AssertThat(resolved).IsEqual(gameSettings);
        Assertions.AssertFloat(resolved.MovementSpeed).IsEqual(5.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RegisterServices_WithNullGameSettings_SkipsRegistration()
    {
        // Arrange
        _provider.GameSettings = null;

        // Act
        _provider.RegisterServices(_container);

        // Assert
        Assertions.AssertBool(_container.IsRegistered<IGameSettings>()).IsFalse();
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

    [TestCase]
    [TestCategory("Unit")]
    public void Ready_WithNullGameSettings_StillAddsToGroup()
    {
        // Arrange
        _provider.GameSettings = null;

        // Act
        _provider._Ready();

        // Assert
        Assertions.AssertBool(_provider.IsInGroup("service_providers")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GameSettingsProperty_CanBeSetAndRetrieved()
    {
        // Arrange
        var gameSettings = CreateTestGameSettings();

        // Act
        _provider.GameSettings = gameSettings;

        // Assert
        Assertions.AssertThat(_provider.GameSettings).IsEqual(gameSettings);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RegisterServices_WithValidSettings_UsesCorrectImplementation()
    {
        // Arrange
        var gameSettings = CreateTestGameSettings();
        gameSettings.MovementSpeed = 7.5f;
        gameSettings.MouseSensitivity = 0.15f;
        _provider.GameSettings = gameSettings;

        // Act
        _provider.RegisterServices(_container);

        // Assert
        var resolved = _container.Resolve<IGameSettings>();
        Assertions.AssertFloat(resolved.MovementSpeed).IsEqual(7.5f);
        Assertions.AssertFloat(resolved.MouseSensitivity).IsEqual(0.15f);
    }

    private GameSettings CreateTestGameSettings()
    {
        var settings = new GameSettings
        {
            MovementSpeed = 5.0f,
            JumpVelocity = 10.0f,
            MouseSensitivity = 0.1f,
            MinPitch = -80f,
            MaxPitch = 80f,
            LightIntensity = 1.5f
        };
        Assertions.AddNode(settings);
        return settings;
    }
}