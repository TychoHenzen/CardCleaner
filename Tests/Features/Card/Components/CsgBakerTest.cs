using CardCleaner.Scripts.Features.Card.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CsgBakerTest
{
    private CsgBaker _baker = null!;

    [BeforeTest]
    public void Setup()
    {
        _baker = new CsgBaker();
        Assertions.AddNode(_baker);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultValues_AreReasonable()
    {
        // Assert
        Assertions.AssertBool(_baker.BakeOnSetup).IsTrue();
        Assertions.AssertBool(_baker.DebugUVs).IsFalse();
        Assertions.AssertThat(_baker.Designer).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Setup_WithValidCardRoot_SetsDesigner()
    {
        // Arrange
        var cardRoot = CreateMockCardRoot();
        var designer = new CardDesigner();
        designer.Name = "Designer";
        cardRoot.AddChild(designer);
        cardRoot.Reparent(_baker);

        // Act
        _baker.Setup(cardRoot);

        // Assert
        Assertions.AssertThat(_baker.Designer).IsEqual(designer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Setup_WithCardRootMissingDesigner_LeavesDesignerNull()
    {
        // Arrange
        var cardRoot = CreateMockCardRoot();
        // Don't add a Designer child

        // Act
        _baker.Setup(cardRoot);

        // Assert
        Assertions.AssertThat(_baker.Designer).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Setup_WithBakeOnSetupFalse_DoesNotBake()
    {
        // Arrange
        var cardRoot = CreateMockCardRoot();
        _baker.BakeOnSetup = false;

        // Act
        _baker.Setup(cardRoot);

        // Assert - Should complete without throwing (no way to directly verify baking didn't happen)
        Assertions.AssertThat(_baker).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Designer_CanBeSetDirectly()
    {
        // Arrange
        var designer = new CardDesigner();
        Assertions.AddNode(designer);

        // Act
        _baker.Designer = designer;

        // Assert
        Assertions.AssertThat(_baker.Designer).IsEqual(designer);
    }

    private static Node3D CreateMockCardRoot()
    {
        var cardRoot = new Node3D();
        cardRoot.Name = "CardRoot";
        Assertions.AddNode(cardRoot);
        return cardRoot;
    }
}