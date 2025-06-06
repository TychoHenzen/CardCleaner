// Tests/Core/Data/GameSettingsTest.cs
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using GdUnit4;

namespace CardCleaner.Tests.Core.Data;

[TestSuite]
[RequireGodotRuntime]
public class GameSettingsTest
{
    private GameSettings _settings = null!;

    [BeforeTest]
    public void Setup()
    {
        _settings = new GameSettings();
        Assertions.AddNode(_settings);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultValues_AreReasonable()
    {
        // Assert - Verify sensible defaults
        Assertions.AssertFloat(_settings.MovementSpeed).IsEqual(5.0f);
        Assertions.AssertFloat(_settings.JumpVelocity).IsEqual(10.0f);
        Assertions.AssertFloat(_settings.MouseSensitivity).IsEqual(0.1f);
        Assertions.AssertFloat(_settings.MinPitch).IsEqual(-80f);
        Assertions.AssertFloat(_settings.MaxPitch).IsEqual(80f);
        Assertions.AssertThat(_settings.CurrentLightMode).IsEqual(LightMode.Off);
        Assertions.AssertFloat(_settings.LightIntensity).IsEqual(1.5f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Properties_CanBeModified()
    {
        // Act
        _settings.MovementSpeed = 8.0f;
        _settings.MouseSensitivity = 0.2f;
        _settings.CurrentLightMode = LightMode.Blacklight;
        _settings.LightIntensity = 2.0f;

        // Assert
        Assertions.AssertFloat(_settings.MovementSpeed).IsEqual(8.0f);
        Assertions.AssertFloat(_settings.MouseSensitivity).IsEqual(0.2f);
        Assertions.AssertThat(_settings.CurrentLightMode).IsEqual(LightMode.Blacklight);
        Assertions.AssertFloat(_settings.LightIntensity).IsEqual(2.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PitchBounds_AreValid()
    {
        // Assert - Min should be less than max
        Assertions.AssertThat(_settings.MinPitch).IsLess(_settings.MaxPitch);
        
        // Should be reasonable camera bounds
        Assertions.AssertThat(_settings.MinPitch).IsBetween(-90f, 0f);
        Assertions.AssertThat(_settings.MaxPitch).IsBetween(0f, 90f);
    }
}