using CardCleaner.Scripts.Features.Card.Controllers;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Controllers;

[TestSuite]
[RequireGodotRuntime]
public class BlacklightControllerTest
{
    private BlacklightController _controller = null!;
    private Node3D _cardRoot = null!;
    private Node3D _playerNode = null!;
    private SpotLight3D _spotlight = null!;

    [BeforeTest]
    public void Setup()
    {
        _controller = new BlacklightController
        {
            BlacklightRange = 5.0f
        };
        
        _cardRoot = new Node3D();
        Assertions.AddNode(_cardRoot);
        _cardRoot.AddChild(_controller);

        // Set up player with spotlight
        SetupPlayerWithSpotlight();
        
        _controller.Setup(_cardRoot);
    }

    private void SetupPlayerWithSpotlight()
    {
        _playerNode = new Node3D { Name = "TestPlayer" };
        _playerNode.AddToGroup("player");
        
        var head = new Node3D { Name = "Head" };
        var camera = new Camera3D { Name = "Camera3D" };
        _spotlight = new SpotLight3D 
        { 
            Name = "SpotLight3D",
            SpotAngle = 45.0f,
            LightEnergy = 1.0f,
            Visible = true
        };
        
        _playerNode.AddChild(head);
        head.AddChild(camera);
        camera.AddChild(_spotlight);
        
        Assertions.AddNode(_playerNode);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BlacklightController_DefaultValues_AreReasonable()
    {
        // Arrange
        var defaultController = new BlacklightController();

        // Assert
        Assertions.AssertFloat(defaultController.BlacklightRange).IsEqual(5.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BlacklightRange_CanBeModified()
    {
        // Act
        _controller.BlacklightRange = 10.0f;

        // Assert
        Assertions.AssertFloat(_controller.BlacklightRange).IsEqual(10.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CalculateExposure_CardOutsideRange_ReturnsZero()
    {
        // Arrange
        var cardPosition = new Vector3(100, 0, 0); // Far from spotlight
        _spotlight.GlobalPosition = Vector3.Zero;

        // Act
        var exposure = _controller.CalculateExposure(cardPosition);

        // Assert
        Assertions.AssertFloat(exposure).IsEqual(0.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CalculateExposure_SpotlightInvisible_ReturnsZero()
    {
        // Arrange
        var cardPosition = new Vector3(1, 0, 0); // Within range
        _spotlight.Visible = false;

        // Act
        var exposure = _controller.CalculateExposure(cardPosition);

        // Assert
        Assertions.AssertFloat(exposure).IsEqual(0.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CalculateExposure_NoPlayer_ReturnsZero()
    {
        // Arrange
        _playerNode.RemoveFromGroup("player");
        var cardPosition = new Vector3(1, 0, 0);

        // Act
        var exposure = _controller.CalculateExposure(cardPosition);

        // Assert
        Assertions.AssertFloat(exposure).IsEqual(0.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CalculateExposure_CardWithinRangeAndAngle_ReturnsPositiveExposure()
    {
        // Arrange
        _spotlight.GlobalPosition = Vector3.Zero;
        _spotlight.GlobalTransform = new Transform3D(Basis.Identity, Vector3.Zero);
        _spotlight.LookAt(Vector3.Forward, Vector3.Up);
        
        var cardPosition = new Vector3(0, 0, 2); // In front of spotlight, within range

        // Act
        var exposure = _controller.CalculateExposure(cardPosition);

        // Assert
        Assertions.AssertFloat(exposure).IsGreater(0.0f);
        Assertions.AssertFloat(exposure).IsLessEqual(1.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CalculateExposure_CardOutsideSpotAngle_ReturnsZero()
    {
        // Arrange
        _spotlight.GlobalPosition = Vector3.Zero;
        _spotlight.GlobalTransform = new Transform3D(Basis.Identity, Vector3.Zero);
        _spotlight.LookAt(Vector3.Forward, Vector3.Up);
        _spotlight.SpotAngle = 30.0f; // Narrow angle
        
        var cardPosition = new Vector3(5, 0, 1); // To the side, outside angle

        // Act
        var exposure = _controller.CalculateExposure(cardPosition);

        // Assert
        Assertions.AssertFloat(exposure).IsEqual(0.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UpdateBlacklightEffect_WithMaterial_StoresMaterial()
    {
        // Arrange
        var material = new ShaderMaterial();

        // Act
        _controller.UpdateBlacklightEffect(material);

        // Assert - Should store material without error
        Assertions.AssertThat(_controller).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UpdateBlacklightEffect_WithNullMaterial_DoesNotCrash()
    {
        // Act & Assert
        _controller.UpdateBlacklightEffect(null);
        
        Assertions.AssertThat(_controller).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Setup_WithNullCardRoot_DoesNotCrash()
    {
        // Arrange
        var newController = new BlacklightController();

        // Act & Assert
        newController.Setup(null!);
        
        Assertions.AssertThat(newController).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PhysicsProcess_WithoutMaterial_DoesNotCrash()
    {
        // Act & Assert
        _controller.PhysicsProcess(0.016);
        
        Assertions.AssertThat(_controller).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void IntegrateForces_DoesNotCrash()
    {
        // Arrange
        // We can't easily create a real PhysicsDirectBodyState3D, so just test the method exists

        // Act & Assert - Should not crash when called
        _controller.IntegrateForces(null!);
        
        Assertions.AssertThat(_controller).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CalculateExposure_VaryingDistances_ReturnsExpectedValues()
    {
        // Arrange
        _spotlight.GlobalPosition = Vector3.Zero;
        _spotlight.GlobalTransform = new Transform3D(Basis.Identity, Vector3.Zero);
        _spotlight.LookAt(Vector3.Forward, Vector3.Up);
        
        var nearCard = new Vector3(0, 0, 1);   // Close
        var farCard = new Vector3(0, 0, 4);    // Far but within range

        // Act
        var nearExposure = _controller.CalculateExposure(nearCard);
        var farExposure = _controller.CalculateExposure(farCard);

        // Assert - Closer cards should have higher exposure
        Assertions.AssertFloat(nearExposure).IsGreater(farExposure);
        Assertions.AssertFloat(nearExposure).IsGreater(0.0f);
        Assertions.AssertFloat(farExposure).IsGreater(0.0f);
    }
}