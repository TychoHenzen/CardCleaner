namespace CardCleaner.Tests.Features.Card.Components.CardHolderScenarios;

/// <summary>
///     Tests for CardHolder card removal split out of CardHolderTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardHolderRemoveTest : CardHolderTestBase
{
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
}
