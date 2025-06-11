using CardCleaner.Scripts.Features.Deckbuilder.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Components;

[TestSuite]
[RequireGodotRuntime]
public class InteractableButtonTest
{
    private InteractableButton _button = null!;
    private MeshInstance3D _mockButtonMesh = null!;
    private MeshInstance3D _mockHighlightMesh = null!;
    private int _buttonPressedEventCount;

    [BeforeTest]
    public void Setup()
    {
        _button = new InteractableButton
        {
            Enabled = true,
            InteractionRange = 5.0f,
            PressDepth = 0.02f,
            PressAnimationSpeed = 0.1f
        };

        // Create mock meshes
        _mockButtonMesh = new MeshInstance3D { Name = "ButtonMesh" };
        _mockHighlightMesh = new MeshInstance3D { Name = "HighlightMesh" };
        
        _button.ButtonMesh = _mockButtonMesh;
        _button.HighlightMesh = _mockHighlightMesh;

        // Add collision shape
        var collisionShape = new CollisionShape3D();
        collisionShape.Shape = new BoxShape3D();
        _button.AddChild(collisionShape);

        Assertions.AddNode(_button);
        _button.AddChild(_mockButtonMesh);
        _button.AddChild(_mockHighlightMesh);

        // Reset event tracking
        _buttonPressedEventCount = 0;
        _button.ButtonPressed += OnButtonPressed;

        // Trigger _Ready
        _button._Ready();
    }

    private void OnButtonPressed()
    {
        _buttonPressedEventCount++;
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractableButton_DefaultValues_AreReasonable()
    {
        // Arrange
        var defaultButton = new InteractableButton();
        Assertions.AddNode(defaultButton);

        // Assert
        Assertions.AssertBool(defaultButton.Enabled).IsTrue();
        Assertions.AssertFloat(defaultButton.InteractionRange).IsEqual(5.0f);
        Assertions.AssertFloat(defaultButton.PressDepth).IsEqual(0.02f);
        Assertions.AssertFloat(defaultButton.PressAnimationSpeed).IsEqual(0.1f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractableButton_InitialState_CorrectInterfaceValues()
    {
        // Assert - IInteractable implementation
        Assertions.AssertBool(_button.CanInteract).IsTrue();
        Assertions.AssertThat(_button.InteractionBody).IsEqual(_button);
        Assertions.AssertFloat(_button.InteractionRange).IsEqual(5.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Ready_WithButtonMesh_SetsInitialVisibility()
    {
        // Assert - Button mesh should be initially hidden and highlight should be hidden
        Assertions.AssertBool(_mockButtonMesh.Visible).IsFalse();
        Assertions.AssertBool(_mockHighlightMesh.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Ready_SetsCorrectCollisionLayer()
    {
        // Assert
        Assertions.AssertThat(_button.CollisionLayer).IsEqual(4); // Layer 3 (bit 2^2 = 4)
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CanInteract_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        _button.Enabled = true;

        // Act & Assert
        Assertions.AssertBool(_button.CanInteract).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CanInteract_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        _button.Enabled = false;

        // Act & Assert
        Assertions.AssertBool(_button.CanInteract).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Interact_WhenEnabled_EmitsButtonPressedSignal()
    {
        // Arrange
        _button.Enabled = true;

        // Act
        _button.Interact();

        // Assert
        Assertions.AssertThat(_buttonPressedEventCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Interact_WhenDisabled_DoesNotEmitSignal()
    {
        // Arrange
        _button.Enabled = false;

        // Act
        _button.Interact();

        // Assert
        Assertions.AssertThat(_buttonPressedEventCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Highlight_WhenEnabled_ShowsHighlightMesh()
    {
        // Arrange
        _button.Enabled = true;

        // Act
        _button.Highlight();

        // Assert
        Assertions.AssertBool(_mockHighlightMesh.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Highlight_WhenDisabled_DoesNotShowHighlight()
    {
        // Arrange
        _button.Enabled = false;

        // Act
        _button.Highlight();

        // Assert
        Assertions.AssertBool(_mockHighlightMesh.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ClearHighlight_HidesHighlightMesh()
    {
        // Arrange
        _button.Highlight();

        // Act
        _button.ClearHighlight();

        // Assert
        Assertions.AssertBool(_mockHighlightMesh.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SetEnabled_True_EnablesButton()
    {
        // Arrange
        _button.Enabled = false;

        // Act
        _button.SetEnabled(true);

        // Assert
        Assertions.AssertBool(_button.Enabled).IsTrue();
        Assertions.AssertBool(_button.CanInteract).IsTrue();
        Assertions.AssertBool(_mockButtonMesh.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SetEnabled_False_DisablesButton()
    {
        // Arrange
        _button.Enabled = true;
        _button.Highlight(); // First show highlight

        // Act
        _button.SetEnabled(false);

        // Assert
        Assertions.AssertBool(_button.Enabled).IsFalse();
        Assertions.AssertBool(_button.CanInteract).IsFalse();
        Assertions.AssertBool(_mockButtonMesh.Visible).IsFalse();
        Assertions.AssertBool(_mockHighlightMesh.Visible).IsFalse(); // Should clear highlight
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Properties_CanBeModified()
    {
        // Act
        _button.InteractionRange = 10.0f;
        _button.PressDepth = 0.05f;
        _button.PressAnimationSpeed = 0.2f;

        // Assert
        Assertions.AssertFloat(_button.InteractionRange).IsEqual(10.0f);
        Assertions.AssertFloat(_button.PressDepth).IsEqual(0.05f);
        Assertions.AssertFloat(_button.PressAnimationSpeed).IsEqual(0.2f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Interact_WithNullButtonMesh_DoesNotCrash()
    {
        // Arrange
        _button.ButtonMesh = null;

        // Act & Assert
        _button.Interact();
        
        Assertions.AssertThat(_buttonPressedEventCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Highlight_WithNullHighlightMesh_DoesNotCrash()
    {
        // Arrange
        _button.HighlightMesh = null;

        // Act & Assert
        _button.Highlight();
        _button.ClearHighlight();
        
        Assertions.AssertThat(_button).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MultipleInteractions_EmitMultipleSignals()
    {
        // Act
        _button.Interact();
        _button.Interact();
        _button.Interact();

        // Assert
        Assertions.AssertThat(_buttonPressedEventCount).IsEqual(3);
    }
}