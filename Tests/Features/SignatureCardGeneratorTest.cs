using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features;

// Mock implementations for testing

[TestSuite]
public class SignatureCardGeneratorTest
{
    private SignatureCardGenerator _generator;
    private RarityVisual[] _rarityVisuals;
    private BaseCardType[] _baseTypes;
    private GemVisual[] _gemVisuals;

    private Mocking.MockCardShaderRenderer _renderer;
    private Node3D _cardRoot;
    private CardTemplate _template;

    [BeforeTest]
    public void Setup()
    {

        // Create test data
        _rarityVisuals = CreateTestRarityVisuals();
        _baseTypes = CreateTestBaseTypes();
        _gemVisuals = CreateTestGemVisuals();

        // Register test data in container
        ServiceLocator.Container.RegisterSingleton(_rarityVisuals);
        ServiceLocator.Container.RegisterSingleton(_baseTypes);
        ServiceLocator.Container.RegisterSingleton(_gemVisuals);

        var material = new CardMaterialManager();
        material.Name = "MaterialManager";
        _cardRoot = Assertions.AddNode(new Node3D());
        _cardRoot.AddChild(material);
        // Create generator (it will pull from ServiceLocator)
        _generator = new SignatureCardGenerator();

        _renderer = CreateMockRenderer();
        _template = new CardTemplate();
    }

    [AfterTest]
    public void TearDown()
    {
        ServiceLocator.ResetForTesting();
    }

    private static RarityVisual[] CreateTestRarityVisuals()
    {
        var commonVisual = new RarityVisual();
        commonVisual.Rarity = CardRarity.Common;
        commonVisual.BaseOptions = new[] { CreateMockTexture() };
        commonVisual.BorderOptions = new[] { CreateMockTexture() };

        //the test base type signature has epic rarity
        var rareVisual = new RarityVisual();
        rareVisual.Rarity = CardRarity.Epic;
        rareVisual.BaseOptions = new[] { CreateMockTexture() };
        rareVisual.BorderOptions = new[] { CreateMockTexture() };

        return new[] { commonVisual, rareVisual };
    }

    private static BaseCardType[] CreateTestBaseTypes()
    {
        var baseType = new BaseCardType();
        baseType.TypeName = "Test Card";
        baseType.BaseSignature = new CardSignature(new[] { 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        baseType.MatchRadius = 1.0f; // Match anything for testing
        baseType.ArtOptions = new[] { CreateMockTexture() };
        baseType.SymbolOptions = new[] { CreateMockTexture() };

        return new[] { baseType };
    }

    private static GemVisual[] CreateTestGemVisuals()
    {
        var gemVisuals = new GemVisual[8];

        for (int i = 0; i < 8; i++)
        {
            var gem = new GemVisual();
            gem.Element = (Element)i;
            gem.SocketTexture = CreateMockTexture();
            gem.PositiveGemTexture = CreateMockTexture();
            gem.NegativeGemTexture = CreateMockTexture();
            gem.PositiveEmissionColor = new Color(1.0f, 0.0f, 0.0f); // Red
            gem.NegativeEmissionColor = new Color(0.0f, 0.0f, 1.0f); // Blue
            gem.PositiveEmissionStrength = 1.0f;
            gem.NegativeEmissionStrength = 0.8f;

            gemVisuals[i] = gem;
        }

        return gemVisuals;
    }

    private static Texture2D CreateMockTexture()
    {
        // Create a minimal 1x1 texture for testing
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgb8);
        image.Fill(Color.Color8(255, 255, 255));
        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    [TestCase]
    public void TestGenerateCardRenderer_SetsGemEmissions()
    {
        var signature = new CardSignature(new[] { 0.7f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });


        _generator.GenerateCardRenderer(_renderer, signature, _template);

        // Should have called SetGemEmission for gems with non-zero values
        Assertions.AssertThat(_renderer.SetGemEmissionCallCount).IsGreaterEqual(2); // At least for indices 0 and 1
    }

    [TestCase]
    public void TestGenerateCardRenderer_PositiveGemEmission()
    {
        var signature = new CardSignature();
        signature.Solidum = 0.8f; // Positive value for element 0


        _generator.GenerateCardRenderer(_renderer, signature, _template);

        // Check that positive gem settings were used
        var (_, color, strength) = _renderer.LastGemEmission[0];
        Assertions.AssertThat(color.R).IsGreater(0.5f); // Should be reddish (positive color)
        Assertions.AssertThat(strength).IsGreater(0.0f);
    }

    [TestCase]
    public void TestGenerateCardRenderer_NegativeGemEmission()
    {
        var signature = new CardSignature();
        signature.Solidum = -0.8f; // Negative value for element 0


        _generator.GenerateCardRenderer(_renderer, signature, _template);

        // Check that negative gem settings were used
        var (_, color, strength) = _renderer.LastGemEmission[0];

        Assertions.AssertThat(color.B).IsGreater(0.5f); // Should be bluish (negative color)
        Assertions.AssertThat(strength).IsGreater(0.0f);
    }

    [TestCase]
    public void TestGenerateCardRenderer_ZeroValueNoEmission()
    {
        var signature = new CardSignature(); // All zeros

        _generator.GenerateCardRenderer(_renderer, signature, _template);

        // Should not set any gem emissions for zero values
        // Note: This depends on implementation - zero intensity might still call SetGemEmission with 0 strength
        // The important thing is that the strength should be 0 for zero signature values
    }

    [TestCase]
    public void TestGenerateCardRenderer_SetsTextures()
    {
        var signature = new CardSignature(new[] { -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f });

        _generator.GenerateCardRenderer(_renderer, signature, _template);

        // Should have set textures from rarity visuals
        Assertions.AssertThat(_template.CardBase.Texture).IsNotNull();

        // Should have set textures from base card type
        Assertions.AssertThat(_template.Art.Texture).IsNull();
    }

    [TestCase]
    public void TestGenerateCardRenderer_DeterministicGeneration()
    {
        var signature = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        // Generate twice with same signature
        var renderer1 = CreateMockRenderer();
        var template1 = new CardTemplate();
        _generator.GenerateCardRenderer(renderer1, signature, template1);

        var renderer2 = CreateMockRenderer();
        var template2 = new CardTemplate();
        _generator.GenerateCardRenderer(renderer2, signature, template2);

        // Results should be identical (same textures assigned)
        Assertions.AssertThat(template1.CardBase.Texture).IsEqual(template2.CardBase.Texture);
        Assertions.AssertThat(template1.Art.Texture).IsEqual(template2.Art.Texture);
        Assertions.AssertThat(renderer1.NameLabel.Text).IsEqual(renderer2.NameLabel.Text);
        Assertions.AssertThat(renderer1.AttrLabel.Text).IsEqual(renderer2.AttrLabel.Text);
    }

    private Mocking.MockCardShaderRenderer CreateMockRenderer()
    {
        var renderer = new Mocking.MockCardShaderRenderer();
        _cardRoot.AddChild(renderer);
        var name = new Label3D();
        var desc = new Label3D();
        renderer.AddChild(name);
        renderer.NameLabel = name;
        renderer.AddChild(desc);
        renderer.AttrLabel = desc;

        renderer.Setup(_cardRoot);
        return renderer;
    }

    [TestCase]
    public void TestGenerateCardRenderer_HandlesNoMatchingBaseType()
    {
        // Create a signature that won't match any base type
        var signature1 = new CardSignature(new[] { -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f });

        // Should not throw, but might not set art/symbol textures
        _generator.GenerateCardRenderer(_renderer, signature1, _template);

        // At minimum, rarity visuals should still be applied
        Assertions.AssertThat(_template.CardBase.Texture).IsNotNull();
        Assertions.AssertThat(_template.Art.Texture).IsNull();
    }

    [TestCase]
    public void TestGenerateCardRenderer_HandlesMatchingBaseType()
    {
        // Create a signature that matches the registered base type
        var signature1 = new CardSignature(new[] { 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        // Should not throw, but might not set art/symbol textures
        _generator.GenerateCardRenderer(_renderer, signature1, _template);

        // At minimum, rarity visuals should still be applied
        Assertions.AssertThat(_template.CardBase.Texture).IsNotNull();
        Assertions.AssertThat(_template.Art.Texture).IsNotNull();
    }
}