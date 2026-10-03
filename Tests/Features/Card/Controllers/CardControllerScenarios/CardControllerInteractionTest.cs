using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;

namespace CardCleaner.Tests.Features.Card.Controllers.CardControllerScenarios;

/// <summary>
///     CardController interaction, highlighting and signal scenarios split out of CardControllerTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardControllerInteractionTest : CardControllerTestBase
{
    #region Interaction Tests

    [TestCase]
    [TestCategory("Unit")]
    public void CanInteract_WhenNotHeld_ReturnsTrue()
    {
        // Assert - Card should be interactable when not held
        Assertions.AssertBool(_cardController.CanInteract).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CanInteract_WhenHeld_ReturnsFalse()
    {
        // Arrange - Simulate being held by reparenting to a Camera3D
        var camera = new Camera3D();
        Assertions.AddNode(camera);
        _cardController.Reparent(camera);

        // Assert
        Assertions.AssertBool(_cardController.CanInteract).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Interact_WhenCanInteract_EmitsInteractionRequestedSignal()
    {
        // Act
        _cardController.Interact();

        // Assert
        Assertions.AssertThat(_cardInteractionRequestedEventCount).IsEqual(1);
        Assertions.AssertThat(_lastInteractionRequestedCard).IsEqual(_cardController);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Interact_WhenCannotInteract_DoesNotEmitSignal()
    {
        // Arrange - Make card non-interactable by holding it
        var camera = new Camera3D();
        Assertions.AddNode(camera);
        _cardController.Reparent(camera);

        // Act
        _cardController.Interact();

        // Assert
        Assertions.AssertThat(_cardInteractionRequestedEventCount).IsEqual(0);
    }

    #endregion

    #region Highlighting Tests

    [TestCase]
    [TestCategory("Unit")]
    public void Highlight_ShowsOutlineBox()
    {
        // Act
        _cardController.Highlight();

        // Assert
        var outline = _cardController.GetNode<CsgBox3D>("OutlineBox");
        Assertions.AssertBool(outline.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ClearHighlight_HidesOutlineBox()
    {
        // Arrange
        _cardController.Highlight();

        // Act
        _cardController.ClearHighlight();

        // Assert
        var outline = _cardController.GetNode<CsgBox3D>("OutlineBox");
        Assertions.AssertBool(outline.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Highlight_WithoutOutlineBox_DoesNotCrash()
    {
        // Arrange
        var cardWithoutOutline = new CardController();
        Assertions.AddNode(cardWithoutOutline);

        // Act & Assert
        cardWithoutOutline.Highlight();
        cardWithoutOutline.ClearHighlight();

        Assertions.AssertThat(cardWithoutOutline).IsNotNull();
    }

    #endregion

    #region Signal Emission Tests

    [TestCase]
    [TestCategory("Unit")]
    public void EmitPickupSignal_EmitsCardPickedUpSignal()
    {
        // Act
        _cardController.EmitPickupSignal();

        // Assert
        Assertions.AssertThat(_cardPickedUpEventCount).IsEqual(1);
        Assertions.AssertThat(_lastPickedUpCard).IsEqual(_cardController);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MultipleInteractions_EmitMultipleSignals()
    {
        // Act
        _cardController.Interact();
        _cardController.Interact();
        _cardController.EmitPickupSignal();

        // Assert
        Assertions.AssertThat(_cardInteractionRequestedEventCount).IsEqual(2);
        Assertions.AssertThat(_cardPickedUpEventCount).IsEqual(1);
    }

    #endregion
}
