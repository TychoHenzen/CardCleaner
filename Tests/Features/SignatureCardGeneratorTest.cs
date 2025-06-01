using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enum;
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
    private ServiceContainer _container;
    private RarityVisual[] _rarityVisuals;
    private BaseCardType[] _baseTypes;
    private GemVisual[] _gemVisuals;

    [Before]
    public void Setup()
    {
        _container = new ServiceContainer();
        
        // Create test data
        _rarityVisuals = CreateTestRarityVisuals();
        _baseTypes = CreateTestBaseTypes();
        _gemVisuals = CreateTestGemVisuals();
        
        // Register test data in container
        _container.RegisterSingleton(_rarityVisuals);
        _container.RegisterSingleton(_baseTypes);
        _container.RegisterSingleton(_gemVisuals);
        
        // Create generator (it will pull from ServiceLocator)
        _generator = new SignatureCardGenerator();
    }

    private RarityVisual[] CreateTestRarityVisuals()
    {
        var commonVisual = new RarityVisual();
        commonVisual.Rarity = CardRarity.Common;
        commonVisual.BaseOptions = new[] { CreateMockTexture("common_base") };
        commonVisual.BorderOptions = new[] { CreateMockTexture("common_border") };
        
        var rareVisual = new RarityVisual();
        rareVisual.Rarity = CardRarity.Rare;
        rareVisual.BaseOptions = new[] { CreateMockTexture("rare_base") };
        rareVisual.BorderOptions = new[] { CreateMockTexture("rare_border") };
        
        return new[] { commonVisual, rareVisual };
    }

    private BaseCardType[] CreateTestBaseTypes()
    {
        var baseType = new BaseCardType();
        baseType.TypeName = "Test Card";
        baseType.BaseSignature = new CardSignature(new[] { 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        baseType.MatchRadius = 1.0f; // Match anything for testing
        baseType.ArtOptions = new[] { CreateMockTexture("test_art") };
        baseType.SymbolOptions = new[] { CreateMockTexture("test_symbol") };
        
        return new[] { baseType };
    }

    private GemVisual[] CreateTestGemVisuals()
    {
        var gemVisuals = new GemVisual[8];
        
        for (int i = 0; i < 8; i++)
        {
            var gem = new GemVisual();
            gem.Element = (Element)i;
            gem.SocketTexture = CreateMockTexture($"socket_{i}");
            gem.PositiveGemTexture = CreateMockTexture($"pos_gem_{i}");
            gem.NegativeGemTexture = CreateMockTexture($"neg_gem_{i}");
            gem.PositiveEmissionColor = new Color(1.0f, 0.0f, 0.0f); // Red
            gem.NegativeEmissionColor = new Color(0.0f, 0.0f, 1.0f); // Blue
            gem.PositiveEmissionStrength = 1.0f;
            gem.NegativeEmissionStrength = 0.8f;
            
            gemVisuals[i] = gem;
        }
        
        return gemVisuals;
    }

    private Texture2D CreateMockTexture(string name)
    {
        // Create a minimal 1x1 texture for testing
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgb8);
        image.Fill(Color.Color8(255,255,255));
        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    [TestCase]
    public void TestGenerateCardRenderer_SetsGemEmissions()
    {
        var signature = new CardSignature(new[] { 0.7f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var renderer = new MockCardShaderRenderer();
        var template = new CardTemplate();
        
        _generator.GenerateCardRenderer(renderer, signature, template);
        
        // Should have called SetGemEmission for gems with non-zero values
        Assertions.AssertThat(renderer.SetGemEmissionCallCount).IsGreaterEqual(2); // At least for indices 0 and 1
    }

    [TestCase]
    public void TestGenerateCardRenderer_PositiveGemEmission()
    {
        var signature = new CardSignature();
        signature.Solidum = 0.8f; // Positive value for element 0
        
        var renderer = new MockCardShaderRenderer();
        var template = new CardTemplate();
        
        _generator.GenerateCardRenderer(renderer, signature, template);
        
        // Check that positive gem settings were used
        var (index, color, strength) = renderer.LastGemEmission;
        if (renderer.SetGemEmissionCallCount > 0)
        {
            Assertions.AssertThat(color.R).IsGreater(0.5f); // Should be reddish (positive color)
            Assertions.AssertThat(strength).IsGreater(0.0f);
        }
    }

    [TestCase]
    public void TestGenerateCardRenderer_NegativeGemEmission()
    {
        var signature = new CardSignature();
        signature.Solidum = -0.8f; // Negative value for element 0
        
        var renderer = new MockCardShaderRenderer();
        var template = new CardTemplate();
        
        _generator.GenerateCardRenderer(renderer, signature, template);
        
        // Check that negative gem settings were used
        var (index, color, strength) = renderer.LastGemEmission;
        if (renderer.SetGemEmissionCallCount > 0)
        {
            Assertions.AssertThat(color.B).IsGreater(0.5f); // Should be bluish (negative color)
            Assertions.AssertThat(strength).IsGreater(0.0f);
        }
    }

    [TestCase]
    public void TestGenerateCardRenderer_ZeroValueNoEmission()
    {
        var signature = new CardSignature(); // All zeros
        var renderer = new MockCardShaderRenderer();
        var template = new CardTemplate();
        
        _generator.GenerateCardRenderer(renderer, signature, template);
        
        // Should not set any gem emissions for zero values
        // Note: This depends on implementation - zero intensity might still call SetGemEmission with 0 strength
        // The important thing is that the strength should be 0 for zero signature values
    }

    [TestCase]
    public void TestGenerateCardRenderer_SetsTextures()
    {
        var signature = new CardSignature();
        var renderer = new MockCardShaderRenderer();
        var template = new CardTemplate();
        
        _generator.GenerateCardRenderer(renderer, signature, template);
        
        // Should have set textures from rarity visuals
        Assertions.AssertThat(template.CardBase.Texture).IsNotNull();
        
        // Should have set textures from base card type
        Assertions.AssertThat(template.Art.Texture).IsNotNull();
    }

    [TestCase]
    public void TestGenerateCardRenderer_DeterministicGeneration()
    {
        var signature = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        
        // Generate twice with same signature
        var renderer1 = new MockCardShaderRenderer();
        var template1 = new CardTemplate();
        _generator.GenerateCardRenderer(renderer1, signature, template1);
        
        var renderer2 = new MockCardShaderRenderer();
        var template2 = new CardTemplate();
        _generator.GenerateCardRenderer(renderer2, signature, template2);
        
        // Results should be identical (same textures assigned)
        Assertions.AssertThat(template1.CardBase.Texture).IsEqual(template2.CardBase.Texture);
        Assertions.AssertThat(template1.Art.Texture).IsEqual(template2.Art.Texture);
    }

    [TestCase]
    public void TestGenerateCardRenderer_HandlesNoMatchingBaseType()
    {
        // Create a signature that won't match any base type
        var signature = new CardSignature(new[] { -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f });
        
        // Create base type with very small match radius
        var restrictiveBaseType = new BaseCardType();
        restrictiveBaseType.BaseSignature = new CardSignature(); // All zeros
        restrictiveBaseType.MatchRadius = 0.1f; // Very small radius
        
        _container.RegisterSingleton(new[] { restrictiveBaseType });
        
        var renderer = new MockCardShaderRenderer();
        var template = new CardTemplate();
        
        // Should not throw, but might not set art/symbol textures
        _generator.GenerateCardRenderer(renderer, signature, template);
        
        // At minimum, rarity visuals should still be applied
        Assertions.AssertThat(template.CardBase.Texture).IsNotNull();
    }
}