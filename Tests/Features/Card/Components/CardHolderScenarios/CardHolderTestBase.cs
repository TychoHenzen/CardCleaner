using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components.CardHolderScenarios;

/// <summary>
///     Shared fixture and builders for the CardHolder scenario suites.
/// </summary>
public abstract class CardHolderTestBase
{
    protected CardHolder _systemUnderTest = null!;
    protected Node3D _handAnchor = null!;
    protected Mocking.MockCardSpawner _mockSpawner = null!;

    // Test state tracking
    protected int _cardAddedEventCount;
    protected int _cardRemovedEventCount;
    protected RigidBody3D? _lastCardAdded;
    protected RigidBody3D? _lastCardRemoved;

    [Before]
    public void SetupTestSuite()
    {
        // Setup service container once for all tests
        ServiceLocator.ResetForTesting();
    }

    [BeforeTest]
    public void SetupEachTest()
    {
        // Arrange - Create and configure system dependencies
        _mockSpawner = CreateMockSpawner();
        ServiceLocator.Container.RegisterSingleton<ICardSpawner>(_mockSpawner);

        _handAnchor = CreateHandAnchor();
        _systemUnderTest = CreateCardHolder();

        // Reset event tracking
        ResetEventTracking();
        ConnectToCardHolderEvents();
    }

    [AfterTest]
    public void CleanupEachTest()
    {
        ServiceLocator.ResetForTesting();
    }

    protected CardHolder CreateCardHolder()
    {
        var holder = new CardHolder
        {
            HoldDistance = 2.0f,
            CardCollisionLayer = 2
        };
        Assertions.AddNode(holder);
        holder.SetReferences(_handAnchor);
        return holder;
    }

    protected Node3D CreateHandAnchor()
    {
        var anchor = new Node3D { Name = "HandAnchor" };
        Assertions.AddNode(anchor);
        return anchor;
    }

    protected Mocking.MockCardSpawner CreateMockSpawner()
    {
        var spawner = new Mocking.MockCardSpawner { Name = "MockSpawner" };
        Assertions.AddNode(spawner);
        return spawner;
    }

    protected RigidBody3D CreateTestCard(string cardName)
    {
        var card = new RigidBody3D { Name = cardName };

        var collision = new CollisionShape3D
        {
            Name = "CardCollision",
            Shape = new BoxShape3D()
        };
        card.AddChild(collision);

        var designer = new CardDesigner { Name = "Designer" };
        card.AddChild(designer);

        // Add CardController for pickup signal testing
        var controller = new CardController();
        card.AddChild(controller);

        Assertions.AddNode(card);
        return card;
    }

    protected RigidBody3D[] CreateMultipleTestCards(int count)
    {
        var cards = new RigidBody3D[count];
        for (var i = 0; i < count; i++) cards[i] = CreateTestCard($"Card{i + 1}");
        return cards;
    }

    protected void ConnectToCardHolderEvents()
    {
        _systemUnderTest.CardAdded += OnCardAdded;
        _systemUnderTest.CardRemoved += OnCardRemoved;
    }

    protected void ResetEventTracking()
    {
        _cardAddedEventCount = 0;
        _cardRemovedEventCount = 0;
        _lastCardAdded = null;
        _lastCardRemoved = null;
    }

    protected void OnCardAdded(RigidBody3D card)
    {
        _cardAddedEventCount++;
        _lastCardAdded = card;
    }

    protected void OnCardRemoved(RigidBody3D card)
    {
        _cardRemovedEventCount++;
        _lastCardRemoved = card;
    }
}
