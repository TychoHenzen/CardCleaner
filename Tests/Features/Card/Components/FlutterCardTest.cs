using CardCleaner.Scripts.Features.Card.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class FlutterCardTest
{
    private FlutterCard _flutterCard = null!;

    [BeforeTest]
    public void Setup()
    {
        _flutterCard = new FlutterCard
        {
            AirDensity = 1.0f,
            DragCoeff = 0.5f,
            LiftCoeff = 0.2f,
            FlutterPitch = 0.05f,
            FlutterTwist = 0.1f
        };
        Assertions.AddNode(_flutterCard);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultValues_AreReasonable()
    {
        // Arrange
        var defaultFlutter = new FlutterCard();
        Assertions.AddNode(defaultFlutter);

        // Assert - Default values should be sensible for card physics
        Assertions.AssertFloat(defaultFlutter.AirDensity).IsEqual(1.0f);
        Assertions.AssertFloat(defaultFlutter.DragCoeff).IsEqual(0.5f);
        Assertions.AssertFloat(defaultFlutter.LiftCoeff).IsEqual(0.2f);
        Assertions.AssertFloat(defaultFlutter.FlutterPitch).IsEqual(0.05f);
        Assertions.AssertFloat(defaultFlutter.FlutterTwist).IsEqual(0.1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Properties_CanBeModified()
    {
        // Act
        _flutterCard.AirDensity = 1.5f;
        _flutterCard.DragCoeff = 0.8f;
        _flutterCard.LiftCoeff = 0.3f;
        _flutterCard.FlutterPitch = 0.1f;
        _flutterCard.FlutterTwist = 0.15f;

        // Assert
        Assertions.AssertFloat(_flutterCard.AirDensity).IsEqual(1.5f);
        Assertions.AssertFloat(_flutterCard.DragCoeff).IsEqual(0.8f);
        Assertions.AssertFloat(_flutterCard.LiftCoeff).IsEqual(0.3f);
        Assertions.AssertFloat(_flutterCard.FlutterPitch).IsEqual(0.1f);
        Assertions.AssertFloat(_flutterCard.FlutterTwist).IsEqual(0.15f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Setup_NullCardRoot_DoesNotThrow()
    {
        // Act & Assert - Should handle null gracefully
        _flutterCard.Setup(null!);

        Assertions.AssertThat(_flutterCard).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PhysicsProcess_DoesNotThrow()
    {
        // Act & Assert - Should not throw when called
        _flutterCard.PhysicsProcess(0.016); // Simulate one frame

        Assertions.AssertThat(_flutterCard).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AerodynamicCoefficients_AcceptValidRanges()
    {
        // Act - Test boundary values
        _flutterCard.DragCoeff = 0.0f;
        _flutterCard.LiftCoeff = 0.0f;
        _flutterCard.AirDensity = 0.1f;

        // Assert
        Assertions.AssertFloat(_flutterCard.DragCoeff).IsEqual(0.0f);
        Assertions.AssertFloat(_flutterCard.LiftCoeff).IsEqual(0.0f);
        Assertions.AssertFloat(_flutterCard.AirDensity).IsEqual(0.1f);

        // Act - Test higher values
        _flutterCard.DragCoeff = 2.0f;
        _flutterCard.LiftCoeff = 1.5f;
        _flutterCard.AirDensity = 3.0f;

        // Assert
        Assertions.AssertFloat(_flutterCard.DragCoeff).IsEqual(2.0f);
        Assertions.AssertFloat(_flutterCard.LiftCoeff).IsEqual(1.5f);
        Assertions.AssertFloat(_flutterCard.AirDensity).IsEqual(3.0f);
    }
}