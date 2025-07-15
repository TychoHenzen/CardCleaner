using CardCleaner.Scripts.Core.Utilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Core.Utilities;

[TestSuite]
[RequireGodotRuntime]
public class CardSleepEnforcerTest
{
    private CardSleepEnforcer _enforcer = null!;

    [BeforeTest]
    public void Setup()
    {
        _enforcer = new CardSleepEnforcer
        {
            LinearSleepThreshold = 0.05f,
            AngularSleepThreshold = 0.05f
        };
        Assertions.AddNode(_enforcer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultThresholds_AreReasonable()
    {
        // Arrange
        var defaultEnforcer = new CardSleepEnforcer();
        Assertions.AddNode(defaultEnforcer);

        // Assert - Default thresholds should be sensible for card physics
        Assertions.AssertFloat(defaultEnforcer.LinearSleepThreshold).IsEqual(0.05f);
        Assertions.AssertFloat(defaultEnforcer.AngularSleepThreshold).IsEqual(0.05f);
        Assertions.AssertThat(defaultEnforcer.LinearSleepThreshold).IsGreater(0.0f);
        Assertions.AssertThat(defaultEnforcer.AngularSleepThreshold).IsGreater(0.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ThresholdProperties_CanBeModified()
    {
        // Act
        _enforcer.LinearSleepThreshold = 0.1f;
        _enforcer.AngularSleepThreshold = 0.08f;

        // Assert
        Assertions.AssertFloat(_enforcer.LinearSleepThreshold).IsEqual(0.1f);
        Assertions.AssertFloat(_enforcer.AngularSleepThreshold).IsEqual(0.08f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Setup_NullCardRoot_DoesNotThrow()
    {
        // Act & Assert - Should handle null gracefully
        _enforcer.Setup(null!);

        Assertions.AssertThat(_enforcer).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PhysicsProcess_DoesNotThrow()
    {
        // Act & Assert - Should not throw when called without setup
        _enforcer.PhysicsProcess(0.016); // Simulate one frame

        Assertions.AssertThat(_enforcer).IsNotNull();
    }
}