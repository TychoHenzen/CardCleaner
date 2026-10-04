using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Card.Controllers.CardControllerScenarios;

/// <summary>
///     Shared fixture for the CardController scenario suites.
/// </summary>
public abstract class CardControllerTestBase
{
    protected CardController _cardController = null!;
    protected CardSignature _testSignature = null!;
    protected int _cardPickedUpEventCount;
    protected int _cardInteractionRequestedEventCount;
    protected CardController? _lastPickedUpCard;
    protected CardController? _lastInteractionRequestedCard;

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

    protected void SetupCardComponents()
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

    protected void ConnectToCardEvents()
    {
        _cardController.CardPickedUp += OnCardPickedUp;
        _cardController.CardInteractionRequested += OnCardInteractionRequested;
    }

    protected void ResetEventTracking()
    {
        _cardPickedUpEventCount = 0;
        _cardInteractionRequestedEventCount = 0;
        _lastPickedUpCard = null;
        _lastInteractionRequestedCard = null;
    }

    protected void OnCardPickedUp(CardController card)
    {
        _cardPickedUpEventCount++;
        _lastPickedUpCard = card;
    }

    protected void OnCardInteractionRequested(CardController card)
    {
        _cardInteractionRequestedEventCount++;
        _lastInteractionRequestedCard = card;
    }
}
