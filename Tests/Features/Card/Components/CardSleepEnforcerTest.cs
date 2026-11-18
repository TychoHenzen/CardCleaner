using CardCleaner.Scripts.Features.Card.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardSleepEnforcerTest
{
    private const string CardShaderScene = "res://Scenes/Components/CardShader.tscn";
    private const float Step = 1f / 60f;

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
    public static void DefaultThresholds_AreReasonable()
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
    public void Setup_NullCardRoot_DoesNotThrow()
    {
        // Act & Assert - Should handle null gracefully
        _enforcer.Setup(null!);

        Assertions.AssertThat(_enforcer).IsNotNull();
    }

    [TestCase]
    public void PhysicsProcess_DoesNotThrow()
    {
        // Act & Assert - Should not throw when called without setup
        _enforcer.PhysicsProcess(0.016); // Simulate one frame

        Assertions.AssertThat(_enforcer).IsNotNull();
    }

    [TestCase]
    public void OneSlowStep_DoesNotFreeze()
    {
        // A landing card is slow on the step the floor stops it, while it is still inside the floor.
        AssertBool(_enforcer.Rest(Vector3.Zero, Vector3.Zero, Step)).IsFalse();
    }

    [TestCase]
    public void SlowForTheRestTime_Freezes()
    {
        var steps = StepsToRest();
        for (var i = 1; i < steps; i++)
            AssertBool(_enforcer.Rest(Vector3.Zero, Vector3.Zero, Step)).IsFalse();

        AssertBool(_enforcer.Rest(Vector3.Zero, Vector3.Zero, Step)).IsTrue();
    }

    [TestCase]
    public void AFastStep_StartsTheRestAgain()
    {
        var steps = StepsToRest();
        for (var i = 1; i < steps; i++)
            _enforcer.Rest(Vector3.Zero, Vector3.Zero, Step);
        AssertBool(_enforcer.Rest(Vector3.Down, Vector3.Zero, Step)).IsFalse();
        AssertBool(_enforcer.Rest(Vector3.Zero, Vector3.Right, Step)).IsFalse();

        for (var i = 1; i < steps; i++)
            AssertBool(_enforcer.Rest(Vector3.Zero, Vector3.Zero, Step)).IsFalse();
        AssertBool(_enforcer.Rest(Vector3.Zero, Vector3.Zero, Step)).IsTrue();
    }

    [TestCase]
    public void AfterAFreeze_TheCardRestsAnew()
    {
        var steps = StepsToRest();
        for (var i = 0; i < steps; i++)
            _enforcer.Rest(Vector3.Zero, Vector3.Zero, Step);

        AssertBool(_enforcer.Rest(Vector3.Zero, Vector3.Zero, Step)).IsFalse();
    }

    [TestCase]
    public static void CardShaderScene_SleepEnforcerNodeRunsTheMovedScript()
    {
        // Arrange - not added to the tree: the check needs only the instanced children, not the card's _Ready
        var card = AutoFree(GD.Load<PackedScene>(CardShaderScene).Instantiate());

        // Assert - the scene's SleepEnforcer node must carry the script from its new folder
        Assertions.AssertBool(card.GetNode("SleepEnforcer") is CardSleepEnforcer).IsTrue();
    }

    // Whole physics steps until the enforcer's rest time is reached, counted the way it adds them up.
    private int StepsToRest()
    {
        var steps = 0;
        for (var rested = 0f; rested < _enforcer.RestSeconds; rested += Step)
            steps++;
        return steps;
    }
}