using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Tests.Mocking;
using GdUnit4;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardDropperTest
{
    private CardDropper _dropper = null!;
    private ICardHolder _mockCardHolder = null!;
    private IDropPreview _mockDropPreview = null!;
    private Camera3D _mockCamera = null!;
    private IInputService _mockInputService = null!;

    // Event tracking
    private int _dropStartedEventCount;
    private int _dropCancelledEventCount;
    private int _dropCompletedEventCount;

    [BeforeTest]
    public void Setup()
    {
        _dropper = new CardDropper();
        _mockCardHolder = Substitute.For<ICardHolder>();
        _mockDropPreview = Substitute.For<IDropPreview>();
        _mockCamera = CreateMockCamera();
        _mockInputService = Substitute.For<IInputService>();

        // Register mock input service
        ServiceLocator.Container.RegisterSingleton(_mockInputService);

        Assertions.AddNode(_dropper);

        // Reset event counters
        ResetEventTracking();
        ConnectToDropperEvents();
    }

    [AfterTest]
    public void TearDown()
    {
        ServiceLocator.ResetForTesting();
    }

    #region helpers

    private void SetupMockCardHolderWithCards(bool hasCards)
    {
        _mockCardHolder.HasCards.Returns(hasCards);
        if (hasCards)
        {
            _mockCardHolder.HeldCount.Returns(3);
            var mockCards = new RigidBody3D[] { new(), new(), new() };
            foreach (var card in mockCards) Assertions.AddNode(card);
            _mockCardHolder.HeldCards.Returns(mockCards);
        }
        else
        {
            _mockCardHolder.HeldCount.Returns(0);
            _mockCardHolder.HeldCards.Returns(Array.Empty<RigidBody3D>());
        }
    }


    private Camera3D CreateMockCamera()
    {
        var camera = new Camera3D();
        Assertions.AddNode(camera);
        return camera;
    }


    private void ConnectToDropperEvents()
    {
        _dropper.DropStarted += OnDropStarted;
        _dropper.DropCancelled += OnDropCancelled;
        _dropper.DropCompleted += OnDropCompleted;
    }

    private void ResetEventTracking()
    {
        _dropStartedEventCount = 0;
        _dropCancelledEventCount = 0;
        _dropCompletedEventCount = 0;
    }

    private void OnDropStarted()
    {
        _dropStartedEventCount++;
    }

    private void OnDropCancelled()
    {
        _dropCancelledEventCount++;
    }

    private void OnDropCompleted()
    {
        _dropCompletedEventCount++;
    }

    #endregion

    [TestCase]
    [TestCategory("Unit")]
    public void CardDropper_InitialState_IsNotPreparingDrop()
    {
        // Assert
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Initialize_WithValidDependencies_SetsReferences()
    {
        // Act
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);

        // Assert - No direct way to verify internal state, but should not throw
        Assertions.AssertThat(_dropper).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StartDropPreparation_WithCards_SetsPreparingDropTrue()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);
        SetupMockCardHolderWithCards(true);

        // Act
        _dropper.StartDropPreparation();

        // Assert
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsTrue();
        Assertions.AssertInt(_dropStartedEventCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StartDropPreparation_WithoutCards_DoesNotStartPreparation()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);
        SetupMockCardHolderWithCards(false);

        // Act
        _dropper.StartDropPreparation();

        // Assert
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
        Assertions.AssertInt(_dropStartedEventCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CancelDropPreparation_WhenPreparing_ResetsState()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);
        SetupMockCardHolderWithCards(true);
        _dropper.StartDropPreparation();
        ResetEventTracking();

        // Act
        _dropper.CancelDropPreparation();

        // Assert
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
        Assertions.AssertInt(_dropCancelledEventCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CancelDropPreparation_WhenNotPreparing_DoesNothing()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);

        // Act
        _dropper.CancelDropPreparation();

        // Assert
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
        Assertions.AssertInt(_dropCancelledEventCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CompleteDropPreparation_WhenPreparing_ResetsStateAndEmitsEvent()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);
        SetupMockCardHolderWithCards(true);
        _dropper.StartDropPreparation();
        ResetEventTracking();

        // Act
        _dropper.CompleteDropPreparation();

        // Assert
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
        Assertions.AssertInt(_dropCompletedEventCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CompleteDropPreparation_WhenNotPreparing_DoesNothing()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);

        // Act
        _dropper.CompleteDropPreparation();

        // Assert
        Assertions.AssertBool(_dropper.IsPreparingDrop).IsFalse();
        Assertions.AssertInt(_dropCompletedEventCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DropSingleCard_WithoutCards_DoesNothing()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);
        SetupMockCardHolderWithCards(false);

        // Act
        _dropper.DropSingleCard();

        // Assert - Should complete without error
        Assertions.AssertThat(_dropper).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UpdateDropPreview_WhenNotPreparing_DoesNothing()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);

        // Act
        _dropper.UpdateDropPreview();

        // Assert - Should complete without error
        Assertions.AssertThat(_dropper).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DropSingleCard_WithCards_CallsCardHolderRemoveTopCard()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);
        SetupMockCardHolderWithCards(true);

        // Act
        _dropper.DropSingleCard();

        // Assert
        _mockCardHolder.Received(1).RemoveTopCard();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StartDropPreparation_WithCards_CallsShowPreviewAndPositionCards()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);
        SetupMockCardHolderWithCards(true);

        // Act
        _dropper.StartDropPreparation();

        // Assert
        _mockDropPreview.Received(1).ShowPreview(true);
        _mockCardHolder.Received(1).PositionCardsForDrop();
    }
}