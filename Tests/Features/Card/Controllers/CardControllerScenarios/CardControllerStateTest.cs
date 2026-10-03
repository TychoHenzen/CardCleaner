using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Card.Controllers.CardControllerScenarios;

/// <summary>
///     CardController initial state, component discovery, physics and signature scenarios split
///     out of CardControllerTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardControllerStateTest : CardControllerTestBase
{
    #region Initial State Tests

    [TestCase]
    [TestCategory("Unit")]
    public void CardController_InitialState_CorrectIInteractableValues()
    {
        // Assert - IInteractable implementation
        Assertions.AssertBool(_cardController.CanInteract).IsTrue();
        Assertions.AssertThat(_cardController.InteractionBody).IsEqual(_cardController);
        Assertions.AssertFloat(_cardController.InteractionRange).IsEqual(50f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardController_InitialState_HasCorrectSignature()
    {
        // Assert
        Assertions.AssertThat(_cardController.Signature).IsEqual(_testSignature);
        Assertions.AssertFloat(_cardController.Signature[0]).IsEqual(0.5f);
        Assertions.AssertFloat(_cardController.Signature[1]).IsEqual(-0.3f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Ready_AddsToCardsGroup()
    {
        // Assert
        Assertions.AssertBool(_cardController.IsInGroup("Cards")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Ready_SetsCorrectCollisionLayer()
    {
        // Assert
        Assertions.AssertThat(_cardController.CollisionLayer).IsEqual(2);
    }

    #endregion

    #region Component Discovery Tests

    [TestCase]
    [TestCategory("Unit")]
    public void Ready_DiscoversCardComponents()
    {
        // Assert - Components should be discovered and set up
        var mockPhysics = _cardController.GetNode<MockPhysicsComponent>("MockPhysics");
        var mockCard = _cardController.GetNode<MockCardComponent>("MockCard");

        Assertions.AssertBool(mockPhysics.SetupCalled).IsTrue();
        Assertions.AssertBool(mockCard.SetupCalled).IsTrue();
        Assertions.AssertThat(mockPhysics.CardRoot).IsEqual(_cardController);
        Assertions.AssertThat(mockCard.CardRoot).IsEqual(_cardController);
    }

    #endregion

    #region Physics Integration Tests

    [TestCase]
    [TestCategory("Unit")]
    public void IntegrateForces_CallsPhysicsComponents()
    {
        // Arrange
        var mockPhysics = _cardController.GetNode<MockPhysicsComponent>("MockPhysics");

        // Act
        _cardController._IntegrateForces(null!); // We can't create real PhysicsDirectBodyState3D

        // Assert
        Assertions.AssertBool(mockPhysics.IntegrateForcesWasCalled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PhysicsProcess_CallsPhysicsComponents()
    {
        // Arrange
        var mockPhysics = _cardController.GetNode<MockPhysicsComponent>("MockPhysics");

        // Act
        _cardController._PhysicsProcess(0.016);

        // Assert
        Assertions.AssertBool(mockPhysics.PhysicsProcessWasCalled).IsTrue();
    }

    #endregion

    #region Signature Tests

    [TestCase]
    [TestCategory("Unit")]
    public void Signature_CanBeModified()
    {
        // Arrange
        var newSignature = new CardSignature(new[] { 1.0f, -1.0f, 0.0f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f });

        // Act
        _cardController.Signature = newSignature;

        // Assert
        Assertions.AssertThat(_cardController.Signature).IsEqual(newSignature);
        Assertions.AssertFloat(_cardController.Signature[0]).IsEqual(1.0f);
        Assertions.AssertFloat(_cardController.Signature[1]).IsEqual(-1.0f);
    }

    #endregion
}
