using Godot;

namespace CardCleaner.Tests.Features.Card.Components.CardHolderScenarios;

/// <summary>
///     Tests for CardHolder initial state, card addition, positioning and ordering split out of CardHolderTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardHolderAddTest : CardHolderTestBase
{
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
}
