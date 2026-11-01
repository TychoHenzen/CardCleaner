using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Models.Effects;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using CardCleaner.Scripts.Core.Enumeration;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Card.Services;

// Mock implementations for testing

[TestSuite]
[RequireGodotRuntime]
public class SignatureCardGeneratorTest
{
    private SignatureCardGenerator _generator = null!;

    private Mocking.MockCardShaderRenderer _renderer = null!;
    private Node3D _cardRoot = null!;
    private CardTemplate _template = null!;

    [BeforeTest]
    public void Setup()
    {
        var material = new CardMaterialManager();
        material.Name = "MaterialManager";
        _cardRoot = Assertions.AddNode(new Node3D());
        _cardRoot.AddChild(material);
        // The generator receives the test data through its constructor
        _generator = SignatureCardTestData.CreateGenerator();

        _renderer = CreateMockRenderer();
        _template = new CardTemplate();
    }

    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    [TestCase]
    public void TestGenerateCardRenderer_SetsGemEmissions()
    {
        var signature = new CardSignature(new[] { 0.7f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });


        _generator.GenerateCardRenderer(_renderer, signature, null, _template);

        // Should have called SetGemEmission for gems with non-zero values
        Assertions.AssertThat(_renderer.SetGemEmissionCallCount).IsGreaterEqual(2); // At least for indices 0 and 1
    }

    [TestCase]
    public void TestGenerateCardRenderer_PositiveGemEmission()
    {
        var signature = new CardSignature();
        signature = signature.With(Element.Solidum, 0.8f); // Positive value for element 0


        _generator.GenerateCardRenderer(_renderer, signature, null, _template);

        // Check that positive gem settings were used
        var (_, color, strength) = _renderer.LastGemEmission[0];
        Assertions.AssertThat(color.R).IsGreater(0.5f); // Should be reddish (positive color)
        Assertions.AssertThat(strength).IsGreater(0.0f);
    }

    [TestCase]
    public void TestGenerateCardRenderer_NegativeGemEmission()
    {
        var signature = new CardSignature();
        signature = signature.With(Element.Solidum, -0.8f); // Negative value for element 0


        _generator.GenerateCardRenderer(_renderer, signature, null, _template);

        // Check that negative gem settings were used
        var (_, color, strength) = _renderer.LastGemEmission[0];

        Assertions.AssertThat(color.B).IsGreater(0.5f); // Should be bluish (negative color)
        Assertions.AssertThat(strength).IsGreater(0.0f);
    }

    [TestCase]
    public void TestGenerateCardRenderer_ZeroValueNoEmission()
    {
        var signature = new CardSignature(); // All zeros

        _generator.GenerateCardRenderer(_renderer, signature, null, _template);

        // Should not set any gem emissions for zero values
        // Note: This depends on implementation - zero intensity might still call SetGemEmission with 0 strength
        // The important thing is that the strength should be 0 for zero signature values
    }

    [TestCase]
    public void TestGenerateCardRenderer_SetsTextures()
    {
        var signature = new CardSignature(new[] { -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f });

        _generator.GenerateCardRenderer(_renderer, signature, null, _template);

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
        _generator.GenerateCardRenderer(renderer1, signature, null, template1);

        var renderer2 = CreateMockRenderer();
        var template2 = new CardTemplate();
        _generator.GenerateCardRenderer(renderer2, signature, null, template2);

        // Results should be identical (same textures assigned)
        Assertions.AssertThat(template1.CardBase.Texture).IsEqual(template2.CardBase.Texture);
        Assertions.AssertThat(template1.Art.Texture).IsEqual(template2.Art.Texture);
        Assertions.AssertThat(renderer1.NameLabel.Text).IsEqual(renderer2.NameLabel.Text);
        Assertions.AssertThat(renderer1.AttrLabel.Text).IsEqual(renderer2.AttrLabel.Text);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CommonCardRendersWithoutSignatureEdges()
    {
        _generator.GenerateCardRenderer(_renderer, new CardSignature(), null, _template);

        foreach (var layer in _template.GemSockets.Concat(_template.Gems))
            Assertions.AssertThat(layer.RenderOnFront).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SpecialCardRendersSignatureEdges()
    {
        var signature = new CardSignature().With(Element.Febris, 0.6f);

        _generator.GenerateCardRenderer(_renderer, signature, null, _template);

        foreach (var layer in _template.GemSockets.Concat(_template.Gems))
            Assertions.AssertThat(layer.RenderOnFront).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CommonCardKeepsEdgeLayerTexturesSoShaderArraysStayAligned()
    {
        _generator.GenerateCardRenderer(_renderer, new CardSignature(), null, _template);

        foreach (var layer in _template.GemSockets.Concat(_template.Gems))
            Assertions.AssertThat(layer.Texture).IsNotNull();
        Assertions.AssertThat(_template.GatherAllLayers().Length).IsEqual(27);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestZeroSignatureStaysCommon()
    {
        var zero = new CardSignature();
        var renderer = Assertions.AddNode(CreateRealRenderer());
        _generator.GenerateCardRenderer(renderer, zero, null, new CardTemplate());
        var material = ApplyMaterial(renderer);

        Assertions.AssertThat(renderer.NameLabel.Text).IsEqual("Common");
        Assertions.AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        Assertions.AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MundaneArtIsPickedFromAllBaseTypesBySeed()
    {
        var generator = SignatureCardTestData.CreateGeneratorWithBaseTypes(3, out var arts);
        var artIds = new System.Collections.Generic.HashSet<ulong>();
        var baseIds = new System.Collections.Generic.HashSet<ulong>();
        for (var seed = 1; seed <= 64; seed++)
        {
            var template = new CardTemplate();
            generator.GenerateCardRenderer(CreateMockRenderer(), new CardSignature(), seed, template);
            artIds.Add(template.Art.Texture!.GetInstanceId());
            baseIds.Add(template.CardBase.Texture!.GetInstanceId());
        }

        // The zero signature matches no base type, so every art here is the seed's pick among all three.
        Assertions.AssertThat(artIds.SetEquals(arts.Select(art => art.GetInstanceId()))).IsTrue();
        // The rarity visuals follow the look seed too.
        Assertions.AssertThat(baseIds.Count).IsGreater(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SameLookSeedGivesTheSameLook()
    {
        var generator = SignatureCardTestData.CreateGeneratorWithBaseTypes(3, out _);
        var first = new CardTemplate();
        generator.GenerateCardRenderer(CreateMockRenderer(), new CardSignature(), 7, first);
        var again = new CardTemplate();
        generator.GenerateCardRenderer(CreateMockRenderer(), new CardSignature(), 7, again);

        Assertions.AssertThat(again.Art.Texture).IsEqual(first.Art.Texture);
        Assertions.AssertThat(again.CardBase.Texture).IsEqual(first.CardBase.Texture);
        Assertions.AssertThat(again.Border.Texture).IsEqual(first.Border.Texture);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NullLookSeedKeepsTheSignatureSeededLook()
    {
        // A card saved before look seeds existed has no seed: its effect seed comes from its signature, as before.
        var zero = new CardSignature();
        var material = GenerateOnRealRenderer(zero, null);

        Assertions.AssertThat((float)material.GetShaderParameter("art_seed")).IsEqual(CardEffectSeed.For(zero));
        var first = new CardTemplate();
        _generator.GenerateCardRenderer(CreateMockRenderer(), zero, null, first);
        var second = new CardTemplate();
        _generator.GenerateCardRenderer(CreateMockRenderer(), zero, null, second);
        Assertions.AssertThat(second.CardBase.Texture).IsEqual(first.CardBase.Texture);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LookSeedIsIgnoredForASpecialCard()
    {
        var signature = new CardSignature().With(Element.Febris, 0.6f);
        var first = new CardTemplate();
        _generator.GenerateCardRenderer(CreateMockRenderer(), signature, 5, first);
        var second = new CardTemplate();
        _generator.GenerateCardRenderer(CreateMockRenderer(), signature, 9, second);

        Assertions.AssertThat(second.Art.Texture).IsEqual(first.Art.Texture);
        Assertions.AssertThat((float)GenerateOnRealRenderer(signature, 5).GetShaderParameter("art_seed"))
            .IsEqual(CardEffectSeed.For(signature));
        Assertions.AssertThat((float)GenerateOnRealRenderer(signature, 9).GetShaderParameter("art_seed"))
            .IsEqual(CardEffectSeed.For(signature));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MundaneSeedDrivesTheEffectSeed()
    {
        var zero = new CardSignature();
        var eleven = GenerateOnRealRenderer(zero, 11);
        var twelve = GenerateOnRealRenderer(zero, 12);

        Assertions.AssertThat((float)eleven.GetShaderParameter("art_seed")).IsEqual(CardEffectSeed.ForLook(11));
        Assertions.AssertThat((float)twelve.GetShaderParameter("art_seed"))
            .IsNotEqual((float)eleven.GetShaderParameter("art_seed"));
        Assertions.AssertThat((int)eleven.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        Assertions.AssertThat((int)eleven.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MundaneCardHidesTheEnergyOrb()
    {
        var generator = SignatureCardTestData.CreateGeneratorWithBaseTypes(3, out _);
        var template = new CardTemplate();
        generator.GenerateCardRenderer(CreateMockRenderer(), new CardSignature(), 3, template);

        foreach (var layer in new[] { template.EnergyContainer, template.EnergyFill1, template.EnergyFill2 })
        {
            Assertions.AssertThat(layer.RenderOnFront).IsFalse();
            Assertions.AssertThat(layer.Texture).IsNotNull();
        }
        foreach (var layer in template.GemSockets.Concat(template.Gems))
            Assertions.AssertThat(layer.RenderOnFront).IsFalse();
        Assertions.AssertThat(template.GatherAllLayers().Length).IsEqual(27);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SpecialCardShowsTheEnergyOrb()
    {
        var signature = new CardSignature().With(Element.Febris, 0.6f);
        var template = new CardTemplate();
        _generator.GenerateCardRenderer(CreateMockRenderer(), signature, null, template);

        foreach (var layer in new[] { template.EnergyContainer, template.EnergyFill1, template.EnergyFill2 })
            Assertions.AssertThat(layer.RenderOnFront).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MundaneCardKeepsTheAttributeLabel()
    {
        var renderer = Assertions.AddNode(CreateRealRenderer());
        _generator.GenerateCardRenderer(renderer, new CardSignature(), 3, new CardTemplate());

        Assertions.AssertThat(renderer.NameLabel.Text).IsEqual("Common");
        Assertions.AssertThat(renderer.AttrLabel.Visible).IsTrue();
        Assertions.AssertThat(renderer.AttrLabel.Text).IsEqual(new CardSignature().ToDebugString());
    }

    /// <summary>Generates a card on a real renderer with the signature and look seed; returns its material.</summary>
    private ShaderMaterial GenerateOnRealRenderer(CardSignature signature, int? lookSeed)
    {
        var renderer = Assertions.AddNode(CreateRealRenderer());
        _generator.GenerateCardRenderer(renderer, signature, lookSeed, new CardTemplate());
        return ApplyMaterial(renderer);
    }

    private static ShaderMaterial ApplyMaterial(CardShaderRenderer renderer)
    {
        return renderer.GetNode<CardMaterialManager>("MaterialManager")
                   .ApplyMaterial(Assertions.AddNode(new MeshInstance3D()))
               ?? throw new System.InvalidOperationException("No material applied");
    }

    /// <summary>A real renderer with its material manager and labels, so art-effect parameters can be read.</summary>
    private static CardShaderRenderer CreateRealRenderer()
    {
        var renderer = new CardShaderRenderer();
        var manager = new CardMaterialManager
        {
            Name = "MaterialManager",
            CardMaterialTemplate = GD.Load<ShaderMaterial>("res://Assets/Materials/CardMaterial.tres")
        };
        renderer.AddChild(manager);
        renderer.NameLabel = new Label3D();
        renderer.AddChild(renderer.NameLabel);
        renderer.AttrLabel = new Label3D();
        renderer.AddChild(renderer.AttrLabel);
        renderer.Setup(renderer);
        return renderer;
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
        _generator.GenerateCardRenderer(_renderer, signature1, null, _template);

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
        _generator.GenerateCardRenderer(_renderer, signature1, null, _template);

        // At minimum, rarity visuals should still be applied
        Assertions.AssertThat(_template.CardBase.Texture).IsNotNull();
        Assertions.AssertThat(_template.Art.Texture).IsNotNull();
    }

    [TestCase]
    public void TestGenerateCardRenderer_WithMockedRenderer()
    {
        var signature = new CardSignature(new[] { 0.7f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        // Use NSubstitute for the renderer interface behavior we care about
        var mockMaterialManager = Substitute.For<ICardMaterialComponent>();

        // Set up the material manager to track calls
        mockMaterialManager.ApplyMaterial(Arg.Any<MeshInstance3D>()).Returns(new ShaderMaterial());

        // Test with real renderer but mock its dependencies
        var realRenderer = new CardShaderRenderer();
        var materialManager = new CardMaterialManager();
        materialManager.Name = "MaterialManager";
        realRenderer.AddChild(materialManager);
        realRenderer.Setup(realRenderer);
        realRenderer.NameLabel = new Label3D();
        realRenderer.AddChild(realRenderer.NameLabel);
        realRenderer.AttrLabel = new Label3D();
        realRenderer.AddChild(realRenderer.AttrLabel);


        Assertions.AddNode(realRenderer);

        _generator.GenerateCardRenderer(realRenderer, signature, null, _template);


        // Verify the expected behavior without relying on custom mock implementations
        Assertions.AssertThat(realRenderer.NameLabel.Text).IsEqual("Rare");
        Assertions.AssertThat(realRenderer.AttrLabel.Text)
            .IsEqual("Signature[0.70, -0.50, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00]");
    }
}
