using CardCleaner.Scripts.Features.Conveyor.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Conveyor.Components;

[TestSuite]
[RequireGodotRuntime]
public class ConveyorBeltTest
{
    private ConveyorBelt _conveyorBelt = null!;

    [BeforeTest]
    public void Setup()
    {
        _conveyorBelt = new ConveyorBelt
        {
            Speed = 5.0f,
            LateralOffsetRange = 1.0f
        };
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultProperties_AreReasonable()
    {
        // Arrange
        var defaultBelt = new ConveyorBelt();

        // Assert - Default speed should be reasonable
        Assertions.AssertFloat(defaultBelt.Speed).IsEqual(5.0f);
        Assertions.AssertFloat(defaultBelt.LateralOffsetRange).IsEqual(1.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Properties_CanBeModified()
    {
        // Act
        _conveyorBelt.Speed = 10.0f;
        _conveyorBelt.LateralOffsetRange = 2.0f;

        // Assert
        Assertions.AssertFloat(_conveyorBelt.Speed).IsEqual(10.0f);
        Assertions.AssertFloat(_conveyorBelt.LateralOffsetRange).IsEqual(2.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Speed_AcceptsValidRange()
    {
        // Act & Assert - Test various speed values
        _conveyorBelt.Speed = 0.1f;
        Assertions.AssertFloat(_conveyorBelt.Speed).IsEqual(0.1f);

        _conveyorBelt.Speed = 20.0f;
        Assertions.AssertFloat(_conveyorBelt.Speed).IsEqual(20.0f);

        _conveyorBelt.Speed = 0.0f;
        Assertions.AssertFloat(_conveyorBelt.Speed).IsEqual(0.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PhysicsProcess_WithoutSetup_DoesNotThrow()
    {
        // Act & Assert - Should not throw when called without proper setup
        _conveyorBelt._PhysicsProcess(0.016);
        
        Assertions.AssertThat(_conveyorBelt).IsNotNull();
    }
}