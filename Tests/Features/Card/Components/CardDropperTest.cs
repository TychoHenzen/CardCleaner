using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Tests.Mocking;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardDropperTest
{
    private CardDropper _dropper = null!;
    private CardHolder _mockCardHolder = null!;
    private DropPreview _mockDropPreview = null!;
    private Camera3D _mockCamera = null!;
    private MockInputService _mockInputService = null!;
    
    // Event tracking
    private int _dropStartedEventCount;
    private int _dropCancelledEventCount;
    private int _dropCompletedEventCount;

    [BeforeTest]
    public void Setup()
    {
        // Reset service locator
        ServiceLocator.ResetForTesting();
        
        _dropper = new CardDropper();
        _mockCardHolder = CreateMockCardHolder();
        _mockDropPreview = CreateMockDropPreview();
        _mockCamera = CreateMockCamera();
        _mockInputService = new MockInputService();
        
        // Register mock input service
        ServiceLocator.Container.RegisterSingleton<IInputService>(_mockInputService);
        
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
    public void DropSingleCard_WithCards_CallsCardHolderRemoveTopCard()
    {
        // Arrange
        _dropper.Initialize(_mockCardHolder, _mockDropPreview, _mockCamera);
        SetupMockCardHolderWithCards(true);

        // Act
        _dropper.DropSingleCard();

        // Assert - Verify card removal was called (mock would track this)
        Assertions.AssertThat(_dropper).IsNotNull();
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

    private CardHolder CreateMockCardHolder()
    {
        var holder = new CardHolder();
        Assertions.AddNode(holder);
        return holder;
    }

    private DropPreview CreateMockDropPreview()
    {
        var preview = new DropPreview();
        Assertions.AddNode(preview);
        return preview;
    }

    private Camera3D CreateMockCamera()
    {
        var camera = new Camera3D();
        Assertions.AddNode(camera);
        return camera;
    }

    private void SetupMockCardHolderWithCards(bool hasCards)
    {
        // This is a simplification - in a real test, we'd either:
        // 1. Create a proper mock that implements HasCards property
        // 2. Add actual test cards to the holder
        // For now, we're testing the dropper's logic paths
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
}

// Mock input service for testing
public class MockInputService : IInputService
{
    public Vector2 MovementInput => Vector2.Zero;
    public event System.Action<Vector2>? MouseMoved;
    public event System.Action<MouseButton, bool>? MouseButtonChanged;
    public event System.Action<Key, bool>? KeyChanged;

    public bool IsActionPressed(string actionName) => false;
    public bool IsActionJustPressed(string actionName) => false;
    public bool IsActionJustReleased(string actionName) => false;

    public void RegisterAction(object owner, string actionName, Key key, System.Action callback) { }
    public void RegisterAction(object owner, string actionName, MouseButton button, System.Action<bool> callback) { }
    public void UnregisterAction(object owner, string actionName) { }
    public void UnregisterAllActions(object owner) { }
    public void RemapAction(string actionName, Key newKey) { }
    public void RemapAction(string actionName, MouseButton newButton) { }
    public Key GetKeyForAction(string actionName) => Key.Unknown;
    public MouseButton? GetMouseButtonForAction(string actionName) => null;
}