using CardCleaner.Scripts.Features.Card.Components;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardShaderRendererTest
{
    private CardShaderRenderer _renderer = null!;

    [BeforeTest]
    public void Setup()
    {
        _renderer = new CardShaderRenderer();
        Assertions.AddNode(_renderer);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardShaderRenderer_CanBeInstantiated()
    {
        // Assert
        Assertions.AssertThat(_renderer).IsNotNull();
        Assertions.AssertThat(_renderer).IsInstanceOf<CardShaderRenderer>();
    }
    
    [TestCase]
    [TestCategory("Unit")]
    public void SetGemEmission_ValidParameters_DoesNotThrow()
    {
        // Arrange
        var validIndex = 3;
        var validColor = new Color(1.0f, 0.5f, 0.0f);
        var validStrength = 1.2f;

        // Act & Assert - Should not throw
        _renderer.SetGemEmission(validIndex, validColor, validStrength);
        
        Assertions.AssertThat(_renderer).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SetGemEmission_BoundaryIndices_DoesNotThrow()
    {
        // Act & Assert - Should handle boundary indices gracefully
        _renderer.SetGemEmission(0, Colors.Red, 1.0f);
        _renderer.SetGemEmission(7, Colors.Blue, 1.0f);
        
        Assertions.AssertThat(_renderer).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SetGemEmission_InvalidIndices_DoesNotThrow()
    {
        // Act & Assert - Should handle invalid indices gracefully
        _renderer.SetGemEmission(-1, Colors.Red, 1.0f);
        _renderer.SetGemEmission(8, Colors.Blue, 1.0f);
        _renderer.SetGemEmission(100, Colors.Green, 1.0f);
        
        Assertions.AssertThat(_renderer).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Bake_NullTemplate_DoesNotThrow()
    {
        // Act & Assert - Should handle null template gracefully
        _renderer.Bake(null!);
        
        Assertions.AssertThat(_renderer).IsNotNull();
    }
}
