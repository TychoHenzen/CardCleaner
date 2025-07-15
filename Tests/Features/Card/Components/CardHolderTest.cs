using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

/// <summary>
/// Tests for CardHolder component focusing on card management and positioning behavior.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardHolderTest
{
    private CardHolder _systemUnderTest = null!;
    private Node3D _handAnchor = null!;
    private Mocking.MockCardSpawner _mockSpawner = null!;

    // Test state tracking
    private int _cardAddedEventCount;
    private int _cardRemovedEventCount;
    private RigidBody3D? _lastCardAdded;
    private RigidBody3D? _lastCardRemoved;

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

    #region Initial State Tests

    [TestCase]
    [TestCategory("Unit")]
    public void CardHolder_InitialState_HasNoCards()
    {
        // Arrange - System is already set up in BeforeTest

        // Act - No action needed for initial state test

        // Assert - Verify initial empty state
        Assertions.AssertInt(_systemUnderTest.HeldCount).IsEqual(0);
        Assertions.AssertBool(_systemUnderTest.HasCards).IsFalse();
        Assertions.AssertThat(_systemUnderTest.HeldCards).IsEmpty();
    }

    #endregion

    #region Add Card Tests

    [TestCase]
    [TestCategory("Unit")]
    public void AddCard_ValidCard_CardIsAddedSuccessfully()
    {
        // Arrange
        var testCard = CreateTestCard("TestCard");

        // Act
        _systemUnderTest.AddCard(testCard);

        // Assert
        Assertions.AssertInt(_systemUnderTest.HeldCount).IsEqual(1);
        Assertions.AssertBool(_systemUnderTest.HasCards).IsTrue();
        Assertions.AssertThat(_systemUnderTest.HeldCards).Contains(testCard);
        Assertions.AssertInt(_cardAddedEventCount).IsEqual(1);
        Assertions.AssertThat(_lastCardAdded).IsEqual(testCard);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AddCard_ValidCard_CardPhysicsDisabled()
    {
        // Arrange
        var testCard = CreateTestCard("TestCard");
        var collision = testCard.GetNode<CollisionShape3D>("CardCollision");

        // Act
        _systemUnderTest.AddCard(testCard);

        // Assert - Verify card physics are properly disabled
        Assertions.AssertBool(testCard.Freeze).IsTrue();
        Assertions.AssertBool(collision.Disabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AddCard_ValidCard_CardReparentedToHandAnchor()
    {
        // Arrange
        var testCard = CreateTestCard("TestCard");

        // Act
        _systemUnderTest.AddCard(testCard);

        // Assert
        Assertions.AssertThat(testCard.GetParent()).IsEqual(_handAnchor);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AddCard_DuplicateCard_CardNotAddedTwice()
    {
        // Arrange
        var testCard = CreateTestCard("TestCard");
        _systemUnderTest.AddCard(testCard);
        ResetEventTracking();

        // Act
        _systemUnderTest.AddCard(testCard); // Attempt to add same card again

        // Assert
        Assertions.AssertInt(_systemUnderTest.HeldCount).IsEqual(1);
        Assertions.AssertInt(_cardAddedEventCount).IsEqual(0); // No additional event
    }

    #endregion

    #region Remove Card Tests

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveCard_ExistingCard_CardRemovedSuccessfully()
    {
        // Arrange
        var testCard = CreateTestCard("TestCard");
        _systemUnderTest.AddCard(testCard);
        ResetEventTracking();

        // Act
        _systemUnderTest.RemoveCard(testCard);

        // Assert
        Assertions.AssertInt(_systemUnderTest.HeldCount).IsEqual(0);
        Assertions.AssertBool(_systemUnderTest.HasCards).IsFalse();
        Assertions.AssertInt(_cardRemovedEventCount).IsEqual(1);
        Assertions.AssertThat(_lastCardRemoved).IsEqual(testCard);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveCard_ExistingCard_CardReparentedToSpawner()
    {
        // Arrange
        var testCard = CreateTestCard("TestCard");
        _systemUnderTest.AddCard(testCard);

        // Act
        _systemUnderTest.RemoveCard(testCard);

        // Assert
        Assertions.AssertThat(testCard.GetParent()).IsEqual(_mockSpawner);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveCard_NonExistentCard_NoChangeToHolder()
    {
        // Arrange
        var cardInHolder = CreateTestCard("CardInHolder");
        var cardNotInHolder = CreateTestCard("CardNotInHolder");
        _systemUnderTest.AddCard(cardInHolder);
        ResetEventTracking();

        // Act
        _systemUnderTest.RemoveCard(cardNotInHolder);

        // Assert
        Assertions.AssertInt(_systemUnderTest.HeldCount).IsEqual(1);
        Assertions.AssertInt(_cardRemovedEventCount).IsEqual(0);
    }

    #endregion

    #region Remove Top Card Tests

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveTopCard_MultipleCards_RemovesLastAddedCard()
    {
        // Arrange
        var firstCard = CreateTestCard("FirstCard");
        var secondCard = CreateTestCard("SecondCard");
        _systemUnderTest.AddCard(firstCard);
        _systemUnderTest.AddCard(secondCard);

        // Act
        _systemUnderTest.RemoveTopCard();

        // Assert
        Assertions.AssertInt(_systemUnderTest.HeldCount).IsEqual(1);
        Assertions.AssertThat(_systemUnderTest.HeldCards).Contains(firstCard);
        Assertions.AssertThat(_systemUnderTest.HeldCards).NotContains(secondCard);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveTopCard_EmptyHolder_NoErrorThrown()
    {
        // Arrange - Holder is already empty from BeforeTest

        // Act & Assert - Should not throw
        _systemUnderTest.RemoveTopCard();

        Assertions.AssertInt(_systemUnderTest.HeldCount).IsEqual(0);
        Assertions.AssertInt(_cardRemovedEventCount).IsEqual(0);
    }

    #endregion

    #region Remove All Cards Tests

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveAllCards_MultipleCards_AllCardsRemoved()
    {
        // Arrange
        var cards = CreateMultipleTestCards(3);
        foreach (var card in cards) _systemUnderTest.AddCard(card);
        ResetEventTracking();

        // Act
        _systemUnderTest.RemoveAllCards();

        // Assert
        Assertions.AssertInt(_systemUnderTest.HeldCount).IsEqual(0);
        Assertions.AssertBool(_systemUnderTest.HasCards).IsFalse();
        Assertions.AssertInt(_cardRemovedEventCount).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveAllCards_EmptyHolder_NoErrorThrown()
    {
        // Arrange - Holder is already empty

        // Act & Assert - Should not throw
        _systemUnderTest.RemoveAllCards();

        Assertions.AssertInt(_cardRemovedEventCount).IsEqual(0);
    }

    #endregion

    #region Card Positioning Tests

    [TestCase]
    [TestCategory("Unit")]
    public void PositionCards_MultipleCards_CardsSeparatedByThickness()
    {
        // Arrange
        var firstCard = CreateTestCard("FirstCard");
        var secondCard = CreateTestCard("SecondCard");
        _systemUnderTest.AddCard(firstCard);
        _systemUnderTest.AddCard(secondCard);

        // Act
        _systemUnderTest.PositionCards();

        // Assert - Cards should be positioned at different Z offsets
        var firstPosition = firstCard.Transform.Origin;
        var secondPosition = secondCard.Transform.Origin;
        Assertions.AssertFloat(firstPosition.Z).IsNotEqual(secondPosition.Z);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PositionCardsForDrop_WithHandAnchorTransform_CardsPositionedRelativeToAnchor()
    {
        // Arrange
        var testCard = CreateTestCard("TestCard");
        var expectedAnchorPosition = new Vector3(0, 5, 0);
        _handAnchor.GlobalTransform = new Transform3D(Basis.Identity, expectedAnchorPosition);
        _systemUnderTest.AddCard(testCard);

        // Act
        _systemUnderTest.PositionCardsForDrop();

        // Assert
        var cardPosition = testCard.GlobalTransform.Origin;
        Assertions.AssertFloat(cardPosition.Y).IsEqual(expectedAnchorPosition.Y);
    }

    #endregion

    #region Card Order Tests

    [TestCase]
    [TestCategory("Unit")]
    public void AddCard_MultipleCards_MaintainsAdditionOrder()
    {
        // Arrange
        var cards = CreateMultipleTestCards(3);

        // Act
        foreach (var card in cards) _systemUnderTest.AddCard(card);

        // Assert
        var heldCards = _systemUnderTest.HeldCards;
        for (var i = 0; i < cards.Length; i++) Assertions.AssertThat(heldCards[i]).IsEqual(cards[i]);
    }

    #endregion

    #region Test Data Builders and Helpers

    private CardHolder CreateCardHolder()
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

    private Node3D CreateHandAnchor()
    {
        var anchor = new Node3D { Name = "HandAnchor" };
        Assertions.AddNode(anchor);
        return anchor;
    }

    private Mocking.MockCardSpawner CreateMockSpawner()
    {
        var spawner = new Mocking.MockCardSpawner { Name = "MockSpawner" };
        Assertions.AddNode(spawner);
        return spawner;
    }

    private RigidBody3D CreateTestCard(string cardName)
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

    private RigidBody3D[] CreateMultipleTestCards(int count)
    {
        var cards = new RigidBody3D[count];
        for (var i = 0; i < count; i++) cards[i] = CreateTestCard($"Card{i + 1}");
        return cards;
    }

    private void ConnectToCardHolderEvents()
    {
        _systemUnderTest.CardAdded += OnCardAdded;
        _systemUnderTest.CardRemoved += OnCardRemoved;
    }

    private void ResetEventTracking()
    {
        _cardAddedEventCount = 0;
        _cardRemovedEventCount = 0;
        _lastCardAdded = null;
        _lastCardRemoved = null;
    }

    private void OnCardAdded(RigidBody3D card)
    {
        _cardAddedEventCount++;
        _lastCardAdded = card;
    }

    private void OnCardRemoved(RigidBody3D card)
    {
        _cardRemovedEventCount++;
        _lastCardRemoved = card;
    }

    #endregion
}