using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class DropPreviewTest
{
    private DropPreview _dropPreview = null!;
    private MeshInstance3D _previewInstance = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _dropPreview = new DropPreview();
        Assertions.AddNode(_dropPreview);

        // Trigger _Ready to initialize the preview components
        await ISceneRunner.SyncPhysicsFrame;
        _previewInstance = _dropPreview.GetNode<MeshInstance3D>("PreviewMesh");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DropPreview_InitialState_PreviewIsHidden()
    {
        //Arrange
        // Assert - Preview should be hidden initially
        Assertions.AssertThat(_previewInstance).IsNotNull();
        Assertions.AssertBool(_previewInstance.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ShowPreview_True_MakesPreviewVisible()
    {
        // Act
        _dropPreview.ShowPreview(true);

        // Assert
        Assertions.AssertBool(_previewInstance.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ShowPreview_False_MakesPreviewHidden()
    {
        // Arrange - First make it visible
        _dropPreview.ShowPreview(true);

        // Act
        _dropPreview.ShowPreview(false);

        // Assert
        Assertions.AssertBool(_previewInstance.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UpdatePreview_ValidOriginAndDirection_UpdatesMesh()
    {
        // Arrange
        var origin = new Vector3(0, 5, 0);
        var direction = Vector3.Down;
        _dropPreview.ShowPreview(true);

        // Act
        _dropPreview.UpdatePreview(origin, direction);

        // Assert - Should complete without error and update the mesh
        Assertions.AssertThat(_previewInstance.Mesh).IsNotNull();
        Assertions.AssertThat(_previewInstance.Mesh).IsInstanceOf<ImmediateMesh>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UpdatePreview_HorizontalDirection_UpdatesMesh()
    {
        // Arrange
        var origin = new Vector3(0, 0, 0);
        var direction = Vector3.Forward;

        // Act
        _dropPreview.UpdatePreview(origin, direction);

        // Assert - Should handle horizontal rays
        Assertions.AssertThat(_previewInstance.Mesh).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UpdatePreview_ZeroDirection_DoesNotCrash()
    {
        // Arrange
        var origin = Vector3.Zero;
        var direction = Vector3.Zero;

        // Act & Assert - Should handle zero direction gracefully
        _dropPreview.UpdatePreview(origin, direction);

        Assertions.AssertThat(_dropPreview).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UpdatePreview_ExtremeValues_DoesNotCrash()
    {
        // Arrange
        var origin = new Vector3(1000, 1000, 1000);
        var direction = new Vector3(-1, -1, -1).Normalized();

        // Act & Assert - Should handle extreme values
        _dropPreview.UpdatePreview(origin, direction);

        Assertions.AssertThat(_dropPreview).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UpdatePreview_MultipleUpdates_HandledCorrectly()
    {
        // Arrange
        _dropPreview.ShowPreview(true);

        // Act - Multiple rapid updates
        _dropPreview.UpdatePreview(Vector3.Zero, Vector3.Down);
        _dropPreview.UpdatePreview(new Vector3(1, 1, 1), Vector3.Up);
        _dropPreview.UpdatePreview(new Vector3(-1, 0, 1), Vector3.Forward);

        // Assert - Should handle multiple updates without issues
        Assertions.AssertThat(_previewInstance.Mesh).IsNotNull();
        Assertions.AssertBool(_previewInstance.Visible).IsTrue();
    }
}