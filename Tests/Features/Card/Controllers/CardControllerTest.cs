using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Controllers;

[TestSuite]
[RequireGodotRuntime]
public class CardControllerTest
{
    private CardController _cardController = null!;
    private CardSignature _testSignature = null!;
    private int _cardPickedUpEventCount;
    private int _cardInteractionRequestedEventCount;
    private CardController? _lastPickedUpCard;
    private CardController? _lastInteractionRequestedCard;

    [BeforeTest]
    public void Setup()
    {
        _cardController = new CardController();
        _testSignature = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        _cardController.Signature = _testSignature;

        // Add required components
        SetupCardComponents();

        Assertions.AddNode(_cardController);

        // Reset event tracking
        ResetEventTracking();
        ConnectToCardEvents();

        // Trigger _Ready to initialize components
        _cardController._Ready();
    }

    private void SetupCardComponents()
    {
        // Add collision shape (required for RigidBody3D)
        var collisionShape = new CollisionShape3D
        {
            Name = "CardCollision",
            Shape = new BoxShape3D()
        };
        _cardController.AddChild(collisionShape);

        // Add outline box for highlighting
        var outlineBox = new CsgBox3D { Name = "OutlineBox", Visible = false };
        _cardController.AddChild(outlineBox);

        // Add some mock card components
        var mockPhysicsComponent = new MockPhysicsComponent { Name = "MockPhysics" };
        _cardController.AddChild(mockPhysicsComponent);

        var mockCardComponent = new MockCardComponent { Name = "MockCard" };
        _cardController.AddChild(mockCardComponent);
    }

    private void ConnectToCardEvents()
    {
        _cardController.CardPickedUp += OnCardPickedUp;
        _cardController.CardInteractionRequested += OnCardInteractionRequested;
    }

    private void ResetEventTracking()
    {
        _cardPickedUpEventCount = 0;
        _cardInteractionRequestedEventCount = 0;
        _lastPickedUpCard = null;
        _lastInteractionRequestedCard = null;
    }

    private void OnCardPickedUp(CardController card)
    {
        _cardPickedUpEventCount++;
        _lastPickedUpCard = card;
    }

    private void OnCardInteractionRequested(CardController card)
    {
        _cardInteractionRequestedEventCount++;
        _lastInteractionRequestedCard = card;
    }

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

// Mock components for testing
public partial class MockCardComponent : Node, ICardComponent
{
    public bool SetupCalled { get; private set; }
    public Node? CardRoot { get; private set; }

    public void Setup(Node cardRoot)
    {
        SetupCalled = true;
        CardRoot = cardRoot;
    }
}

public partial class MockPhysicsComponent : Node, IPhysicsComponent
{
    public bool SetupCalled { get; private set; }
    public Node? CardRoot { get; private set; }
    public bool IntegrateForcesWasCalled { get; private set; }
    public bool PhysicsProcessWasCalled { get; private set; }

    public void Setup(Node cardRoot)
    {
        SetupCalled = true;
        CardRoot = cardRoot;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        IntegrateForcesWasCalled = true;
    }

    public void PhysicsProcess(double delta)
    {
        PhysicsProcessWasCalled = true;
    }
}