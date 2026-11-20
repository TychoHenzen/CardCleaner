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
    public void CanInteract_WhenNotHeld_ReturnsTrue()
    {
        // Assert - Card should be interactable when not held
        Assertions.AssertBool(_cardController.CanInteract).IsTrue();
    }

    [TestCase]
    public void CanInteract_WhenHeld_ReturnsFalse()
    {
        // Arrange - Simulate being held by reparenting to a Camera3D
        var camera = new Camera3D();
        Assertions.AddNode(camera);
        _cardController.Reparent(camera);

        // Assert
        Assertions.AssertBool(_cardController.CanInteract).IsFalse();
    }

    // A click on a card is the hand router's (CardHighlighter); the card on its own does nothing.
    [TestCase]
    public void Interact_OnItsOwn_LeavesTheCardWhereItIs()
    {
        var parent = _cardController.GetParent();

        _cardController.Interact();

        Assertions.AssertObject(_cardController.GetParent()).IsSame(parent);
        Assertions.AssertThat(_cardPickedUpEventCount).IsEqual(0);
    }

    #endregion

    #region Highlighting Tests

    [TestCase]
    public void Highlight_ShowsOutlineBox()
    {
        // Act
        _cardController.Highlight();

        // Assert
        var outline = _cardController.GetNode<MeshInstance3D>("OutlineBox");
        Assertions.AssertBool(outline.Visible).IsTrue();
    }

    [TestCase]
    public void ClearHighlight_HidesOutlineBox()
    {
        // Arrange
        _cardController.Highlight();

        // Act
        _cardController.ClearHighlight();

        // Assert
        var outline = _cardController.GetNode<MeshInstance3D>("OutlineBox");
        Assertions.AssertBool(outline.Visible).IsFalse();
    }

    [TestCase]
    public static void Highlight_WithoutOutlineBox_DoesNotCrash()
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
    public void EmitPickupSignal_EmitsCardPickedUpSignal()
    {
        // Act
        _cardController.EmitPickupSignal();

        // Assert
        Assertions.AssertThat(_cardPickedUpEventCount).IsEqual(1);
        Assertions.AssertThat(_lastPickedUpCard).IsEqual(_cardController);
    }

    [TestCase]
    public void EachPickupSignalIsEmittedOnce()
    {
        _cardController.EmitPickupSignal();
        _cardController.EmitPickupSignal();

        Assertions.AssertThat(_cardPickedUpEventCount).IsEqual(2);
    }

    #endregion
}
