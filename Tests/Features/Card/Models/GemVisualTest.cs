using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models;

[TestSuite]
[RequireGodotRuntime]
public class GemVisualTest
{
    private GemVisual _gemVisual = null!;

    [BeforeTest]
    public void Setup()
    {
        _gemVisual = new GemVisual
        {
            Element = Element.Solidum,
            SocketTexture = CreateMockTexture(),
            PositiveGemTexture = CreateMockTexture(),
            NegativeGemTexture = CreateMockTexture(),
            PositiveEmissionColor = new Color(1.0f, 0.0f, 0.0f),
            NegativeEmissionColor = new Color(0.0f, 0.0f, 1.0f),
            PositiveEmissionStrength = 1.5f,
            NegativeEmissionStrength = 1.2f
        };
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DefaultValues_AreValid()
    {
        // Arrange
        var defaultGem = new GemVisual();

        // Assert
        Assertions.AssertThat(defaultGem.Element).IsEqual(Element.Solidum);
        Assertions.AssertThat(defaultGem.PositiveEmissionColor).IsEqual(Colors.White);
        Assertions.AssertThat(defaultGem.NegativeEmissionColor).IsEqual(Colors.White);
        Assertions.AssertFloat(defaultGem.PositiveEmissionStrength).IsEqual(1.0f);
        Assertions.AssertFloat(defaultGem.NegativeEmissionStrength).IsEqual(1.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Properties_CanBeSet()
    {
        // Assert - Verify all properties were set correctly
        Assertions.AssertThat(_gemVisual.Element).IsEqual(Element.Solidum);
        Assertions.AssertThat(_gemVisual.SocketTexture).IsNotNull();
        Assertions.AssertThat(_gemVisual.PositiveGemTexture).IsNotNull();
        Assertions.AssertThat(_gemVisual.NegativeGemTexture).IsNotNull();

        Assertions.AssertThat(_gemVisual.PositiveEmissionColor.R).IsEqual(1.0f);
        Assertions.AssertThat(_gemVisual.NegativeEmissionColor.B).IsEqual(1.0f);

        Assertions.AssertFloat(_gemVisual.PositiveEmissionStrength).IsEqual(1.5f);
        Assertions.AssertFloat(_gemVisual.NegativeEmissionStrength).IsEqual(1.2f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Element_CanBeChanged()
    {
        // Act
        _gemVisual.Element = Element.Febris;

        // Assert
        Assertions.AssertThat(_gemVisual.Element).IsEqual(Element.Febris);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EmissionStrengths_AcceptValidRange()
    {
        // Act
        _gemVisual.PositiveEmissionStrength = 0.0f;
        _gemVisual.NegativeEmissionStrength = 10.0f;

        // Assert
        Assertions.AssertFloat(_gemVisual.PositiveEmissionStrength).IsEqual(0.0f);
        Assertions.AssertFloat(_gemVisual.NegativeEmissionStrength).IsEqual(10.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EmissionColors_AcceptFullColorRange()
    {
        // Act
        _gemVisual.PositiveEmissionColor = new Color(0.5f, 0.7f, 0.9f, 0.8f);
        _gemVisual.NegativeEmissionColor = Colors.Transparent;

        // Assert
        var posColor = _gemVisual.PositiveEmissionColor;
        Assertions.AssertFloat(posColor.R).IsEqual(0.5f);
        Assertions.AssertFloat(posColor.G).IsEqual(0.7f);
        Assertions.AssertFloat(posColor.B).IsEqual(0.9f);
        Assertions.AssertFloat(posColor.A).IsEqual(0.8f);

        Assertions.AssertThat(_gemVisual.NegativeEmissionColor).IsEqual(Colors.Transparent);
    }

    private static Texture2D CreateMockTexture()
    {
        var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgb8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
    }
}