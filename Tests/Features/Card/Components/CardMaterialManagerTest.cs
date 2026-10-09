using System;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Models.Effects;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

[TestSuite]
[RequireGodotRuntime]
public class CardMaterialManagerTest
{
    private CardMaterialManager _manager = null!;
    private ShaderMaterial _mockTemplate = null!;

    [BeforeTest]
    public void Setup()
    {
        _manager = new CardMaterialManager();
        _mockTemplate = new ShaderMaterial();
        _manager.CardMaterialTemplate = _mockTemplate;
        Assertions.AddNode(_manager);
    }

    [TestCase]
    public void TestSetLayerTextures_EmptyArray()
    {
        var emptyLayers = new LayerData[0];

        _manager.SetLayerTextures(emptyLayers);

        // Should not crash with empty array
        Assertions.AssertThat(_manager).IsNotNull();
    }

    [TestCase]
    public void TestSetLayerTextures_SingleLayer()
    {
        var texture = CreateMockTexture();
        var layer = new LayerData
        {
            Texture = texture,
            Region = new Rect2(0.1f, 0.2f, 0.5f, 0.6f),
            RenderOnFront = true,
            RenderOnBack = false
        };

        _manager.SetLayerTextures(new[] { layer });

        // Verify internal state through ApplyMaterial
        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var material = _manager.ApplyMaterial(meshInstance);

        Assertions.AssertThat(material).IsNotNull();
        Assertions.AssertThat(meshInstance.MaterialOverride).IsEqual(material);
    }

    [TestCase]
    public void TestSetLayerTextures_MultipleLayers()
    {
        var layers = new LayerData[]
        {
            new() { Texture = CreateMockTexture(), Region = new Rect2(0, 0, 1, 1), RenderOnFront = true },
            new() { Texture = CreateMockTexture(), Region = new Rect2(0.5f, 0.5f, 0.5f, 0.5f), RenderOnBack = true },
            new() { Texture = null, Region = new Rect2(0.1f, 0.1f, 0.8f, 0.8f), RenderOnFront = true }
        };

        _manager.SetLayerTextures(layers);

        // Should handle null textures gracefully
        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var material = _manager.ApplyMaterial(meshInstance);
        Assertions.AssertThat(material).IsNotNull();
    }

    [TestCase]
    public void TestSetGemEmission_ValidIndex()
    {
        var color = new Color(1.0f, 0.0f, 0.0f);
        var strength = 0.8f;

        _manager.SetGemEmission(3, color, strength);

        // Test that gem emission can be set multiple times
        _manager.SetGemEmission(7, new Color(0.0f, 1.0f, 0.0f), 0.5f);

        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var material = _manager.ApplyMaterial(meshInstance);
        Assertions.AssertThat(material).IsNotNull();
    }

    [TestCase]
    public void TestSetGemEmission_InvalidIndices()
    {
        // Should handle invalid indices gracefully
        _manager.SetGemEmission(-1, Colors.Red, 1.0f);
        _manager.SetGemEmission(8, Colors.Blue, 1.0f);
        _manager.SetGemEmission(100, Colors.Green, 1.0f);

        // Should not crash
        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var material = _manager.ApplyMaterial(meshInstance);
        Assertions.AssertThat(material).IsNotNull();
    }

    [TestCase]
    public void TestSetGemEmission_BoundaryValues()
    {
        // Test boundary indices
        _manager.SetGemEmission(0, Colors.Red, 0.0f);
        _manager.SetGemEmission(7, Colors.Blue, 10.0f);

        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var material = _manager.ApplyMaterial(meshInstance);
        Assertions.AssertThat(material).IsNotNull();
    }

    [TestCase]
    public void TestSetGemEmission_InitializesArraysOnFirstCall()
    {
        // First call should initialize the arrays
        _manager.SetGemEmission(0, Colors.Red, 1.0f);

        // Second call should reuse existing arrays
        _manager.SetGemEmission(1, Colors.Blue, 0.5f);

        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var material = _manager.ApplyMaterial(meshInstance);
        Assertions.AssertThat(material).IsNotNull();
    }

    [TestCase]
    public void TestApplyMaterial_NullTemplate()
    {
        _manager.CardMaterialTemplate = null!;
        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var result = _manager.ApplyMaterial(meshInstance);

        Assertions.AssertThat(result).IsNull();
        Assertions.AssertThat(meshInstance.MaterialOverride).IsNull();
    }

    [TestCase]
    public void TestApplyMaterial_SetsOverride()
    {
        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var result = _manager.ApplyMaterial(meshInstance);

        Assertions.AssertThat(result).IsNotNull();
        Assertions.AssertThat(meshInstance.MaterialOverride).IsEqual(result);
        // Test that it's actually a different instance from the template
        Assertions.AssertThat(result).IsNotSame(_mockTemplate);
    }

    [TestCase]
    public void TestApplyMaterial_WithParametersSet()
    {
        // Set up some parameters first
        var layer = new LayerData { Texture = CreateMockTexture(), RenderOnFront = true };
        _manager.SetLayerTextures(new[] { layer });
        _manager.SetGemEmission(0, Colors.Red, 1.0f);

        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var result = _manager.ApplyMaterial(meshInstance);

        Assertions.AssertThat(result).IsNotNull();
        Assertions.AssertThat(meshInstance.MaterialOverride).IsEqual(result);
    }

    [TestCase]
    public void TestCombinedLayersAndGems()
    {
        // Test realistic scenario with both layers and gems
        var layers = new LayerData[]
        {
            new() { Texture = CreateMockTexture(), RenderOnFront = true },
            new() { Texture = CreateMockTexture(), RenderOnBack = true }
        };

        _manager.SetLayerTextures(layers);

        // Set multiple gem emissions
        for (var i = 0; i < 8; i++) _manager.SetGemEmission(i, new Color((float)i / 7, 0.5f, 1.0f), (float)i / 10);

        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);

        var material = _manager.ApplyMaterial(meshInstance);
        Assertions.AssertThat(material).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SetArtEffectsReachesTheMaterialAsShaderParameters()
    {
        var normalMap = CreateMockTexture();
        _manager.CardMaterialTemplate = LoadCardMaterial();

        _manager.SetArtEffects(RarityEffect.Foil, ConditionEffect.Shiny, 1234f, normalMap);
        var material = ApplyToNewMesh();

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.Foil);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Shiny);
        AssertThat((float)material.GetShaderParameter("art_seed")).IsEqual(1234f);
        AssertThat(material.GetShaderParameter("art_normal_map").As<Texture2D>()).IsSame(normalMap);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SetArtEffectsWithoutANormalMapLeavesTheShaderDefault()
    {
        _manager.CardMaterialTemplate = LoadCardMaterial();

        _manager.SetArtEffects(RarityEffect.None, ConditionEffect.Worn, 7f, CreateMockTexture());
        _manager.SetArtEffects(RarityEffect.None, ConditionEffect.Worn, 7f, null);
        var material = ApplyToNewMesh();

        AssertThat(material.GetShaderParameter("art_normal_map").Obj).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ArtEffectsSetAfterTheMaterialIsAppliedReachTheAppliedMaterial()
    {
        // A card's bevel map is baked off the main thread, so it can arrive after the material is on the mesh.
        var normalMap = CreateMockTexture();
        _manager.CardMaterialTemplate = LoadCardMaterial();
        _manager.SetArtEffects(RarityEffect.None, ConditionEffect.Worn, 7f, null);
        var material = ApplyToNewMesh();

        _manager.SetArtEffects(RarityEffect.Glow, ConditionEffect.Worn, 7f, normalMap);

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.Glow);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
        AssertThat((float)material.GetShaderParameter("art_seed")).IsEqual(7f);
        AssertThat(material.GetShaderParameter("art_normal_map").As<Texture2D>()).IsSame(normalMap);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ClearingTheNormalMapAfterTheMaterialIsAppliedClearsItOnTheMaterial()
    {
        _manager.CardMaterialTemplate = LoadCardMaterial();
        _manager.SetArtEffects(RarityEffect.Glow, ConditionEffect.Worn, 7f, CreateMockTexture());
        var material = ApplyToNewMesh();

        _manager.SetArtEffects(RarityEffect.None, ConditionEffect.Worn, 7f, null);

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        AssertThat(material.GetShaderParameter("art_normal_map").Obj).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AMapForASupersededRequestIsIgnored()
    {
        _manager.CardMaterialTemplate = LoadCardMaterial();
        var superseded = _manager.SetArtEffects(RarityEffect.None, ConditionEffect.Worn, 7f, null);
        _manager.SetArtEffects(RarityEffect.None, ConditionEffect.Worn, 9f, null);
        var material = ApplyToNewMesh();

        _manager.SetArtNormalMap(superseded, RarityEffect.Glow, CreateMockTexture());

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        AssertThat(material.GetShaderParameter("art_normal_map").Obj).IsNull();
        AssertThat((float)material.GetShaderParameter("art_seed")).IsEqual(9f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AMaterialAppliedWhileTheEffectsAreOffShowsNone()
    {
        _manager.CardMaterialTemplate = LoadCardMaterial();
        _manager.SetArtEffects(RarityEffect.Glow, ConditionEffect.Worn, 7f, CreateMockTexture());
        _manager.ArtEffectsEnabled = false;

        var material = ApplyToNewMesh();

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.None);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.None);

        _manager.ArtEffectsEnabled = true;

        AssertThat((int)material.GetShaderParameter("rarity_effect")).IsEqual((int)RarityEffect.Glow);
        AssertThat((int)material.GetShaderParameter("condition_effect")).IsEqual((int)ConditionEffect.Worn);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TheArtTextureStaysAtItsLayerIndexWhenEarlierLayersHaveNoTexture()
    {
        var template = new CardTemplate();
        foreach (var layer in template.GatherAllLayers())
            layer.Texture = CreateMockTexture();
        template.Symbol.Texture = null;
        template.Banner.Texture = null;
        _manager.CardMaterialTemplate = LoadCardMaterial();
        var artIndex = Array.IndexOf(template.GatherAllLayers(), template.Art);

        _manager.SetLayerTextures(template.GatherAllLayers());
        var material = ApplyToNewMesh();

        var textures = material.GetShaderParameter("textures").As<Godot.Collections.Array<Texture2D>>();
        AssertThat(textures.Count).IsEqual(template.GatherAllLayers().Length);
        AssertThat(textures[artIndex]).IsSame(template.Art.Texture);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ALayerWithoutATextureDrawsNothingInsteadOfShiftingTheOthers()
    {
        var layers = new LayerData[]
        {
            new() { Texture = CreateMockTexture() },
            new() { Texture = null },
            new() { Texture = CreateMockTexture() }
        };
        _manager.CardMaterialTemplate = LoadCardMaterial();

        _manager.SetLayerTextures(layers);
        var material = ApplyToNewMesh();

        var textures = material.GetShaderParameter("textures").As<Godot.Collections.Array<Texture2D>>();
        AssertThat(textures.Count).IsEqual(3);
        AssertThat(textures[2]).IsSame(layers[2].Texture);
        AssertThat(textures[1].GetImage().GetPixel(0, 0).A).IsEqual(0f);
    }

    private ShaderMaterial ApplyToNewMesh()
    {
        var meshInstance = new MeshInstance3D();
        Assertions.AddNode(meshInstance);
        return _manager.ApplyMaterial(meshInstance) ?? throw new InvalidOperationException("No material applied");
    }

    private static ShaderMaterial LoadCardMaterial()
    {
        return GD.Load<ShaderMaterial>("res://Assets/Materials/CardMaterial.tres");
    }

    private static Texture2D CreateMockTexture()
    {
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgb8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
    }
}