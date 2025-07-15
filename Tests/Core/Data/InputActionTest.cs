// Tests/Core/Data/InputActionTest.cs

using System;
using CardCleaner.Scripts.Core.Data;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Core.Data;

[TestSuite]
[RequireGodotRuntime]
public class InputActionTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void Constructor_RequiredProperties_MustBeSet()
    {
        // Arrange
        var owner = new object();
        var callbackExecuted = false;

        // Act
        var action = new InputAction
        {
            Name = "test_action",
            Owner = owner,
            Key = Key.Space,
            Callback = () => callbackExecuted = true
        };

        // Assert
        Assertions.AssertThat(action.Name).IsEqual("test_action");
        Assertions.AssertThat(action.Owner).IsEqual(owner);
        Assertions.AssertThat(action.Key).IsEqual(Key.Space);
        Assertions.AssertThat(action.MouseButton).IsNull();

        // Test callback execution
        action.Callback?.Invoke();
        Assertions.AssertBool(callbackExecuted).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MouseAction_WithCallback_ExecutesCorrectly()
    {
        // Arrange
        var owner = new object();
        var lastPressState = false;
        var callCount = 0;

        var action = new InputAction
        {
            Name = "mouse_action",
            Owner = owner,
            MouseButton = MouseButton.Left,
            MouseCallback = (pressed) =>
            {
                lastPressState = pressed;
                callCount++;
            }
        };

        // Act & Assert
        action.MouseCallback?.Invoke(true);
        Assertions.AssertBool(lastPressState).IsTrue();
        Assertions.AssertThat(callCount).IsEqual(1);

        action.MouseCallback?.Invoke(false);
        Assertions.AssertBool(lastPressState).IsFalse();
        Assertions.AssertThat(callCount).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Matches_Key_ReturnsCorrectValue()
    {
        // Arrange
        var action = new InputAction
        {
            Name = "test",
            Owner = new object(),
            Key = Key.Enter
        };

        // Act & Assert
        Assertions.AssertBool(action.Matches(Key.Enter)).IsTrue();
        Assertions.AssertBool(action.Matches(Key.Space)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Matches_MouseButton_ReturnsCorrectValue()
    {
        // Arrange
        var action = new InputAction
        {
            Name = "test",
            Owner = new object(),
            MouseButton = MouseButton.Right
        };

        // Act & Assert
        Assertions.AssertBool(action.Matches(MouseButton.Right)).IsTrue();
        Assertions.AssertBool(action.Matches(MouseButton.Left)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Matches_WhenKeyIsNull_ReturnsFalse()
    {
        // Arrange
        var action = new InputAction
        {
            Name = "test",
            Owner = new object(),
            MouseButton = MouseButton.Left // Only mouse button set
        };

        // Act & Assert
        Assertions.AssertBool(action.Matches(Key.Space)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Matches_WhenMouseButtonIsNull_ReturnsFalse()
    {
        // Arrange
        var action = new InputAction
        {
            Name = "test",
            Owner = new object(),
            Key = Key.Space // Only key set
        };

        // Act & Assert
        Assertions.AssertBool(action.Matches(MouseButton.Left)).IsFalse();
    }
}