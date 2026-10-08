using CardCleaner.Scripts.Core.Data;
using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardMaterialManager : Node, ICardMaterialComponent
{
    // Stands in for a layer that has no texture, so every other layer keeps its index in the shader's arrays.
    private static readonly Lazy<ImageTexture> EmptyLayer = new(CreateEmptyLayer);

    private readonly Dictionary<string, Variant> _shaderParameters = new();

    private ShaderMaterial _activeMaterial = null!;
    [Export] public ShaderMaterial CardMaterialTemplate { get; set; } = null!;

    public void SetLayerTextures(LayerData[] layers)
    {
        var texturesArr = new Array<Texture2D>();
        var regionsArr = new Array<Vector4>();
        var frontFlagsArr = new Array<bool>();
        var backFlagsArr = new Array<bool>();

        foreach (var layer in layers)
        {
            // The shader finds the art layer by its index in GatherAllLayers, so a layer without a texture must
            // still take its slot.
            texturesArr.Add(layer.Texture ?? EmptyLayer.Value);
            regionsArr.Add(new Vector4(layer.Region.Position.X, layer.Region.Position.Y, layer.Region.Size.X,
                layer.Region.Size.Y));
            frontFlagsArr.Add(layer.RenderOnFront);
            backFlagsArr.Add(layer.RenderOnBack);
        }

        _shaderParameters["textures"] = texturesArr;
        _shaderParameters["regions"] = regionsArr;
        _shaderParameters["frontFlags"] = frontFlagsArr;
        _shaderParameters["backFlags"] = backFlagsArr;
    }

    public void SetGemEmission(int index, Color color, float strength)
    {
        // Ensure arrays exist with proper size
        if (!_shaderParameters.TryGetValue("gem_emission_colors", out var existingColorsVar))
        {
            var colors = new Array<Vector3>();
            var strengths = new Array<float>();

            // Initialize with 8 empty slots
            for (var i = 0; i < 8; i++)
            {
                colors.Add(Vector3.Zero);
                strengths.Add(0.0f);
            }

            _shaderParameters["gem_emission_colors"] = colors;
            _shaderParameters["gem_emission_strengths"] = strengths;
            existingColorsVar = colors;
        }

        var existingColors = existingColorsVar.As<Array<Vector3>>();
        var existingStrengths = _shaderParameters["gem_emission_strengths"].As<Array<float>>();

        if (index is < 0 or >= 8)
        {
            ILog.Error($"Invalid gem index: {index}");
            return;
        }

        existingColors[index] = new Vector3(color.R, color.G, color.B);
        existingStrengths[index] = strength;
    }

    public void SetArtEffects(RarityEffect rarity, ConditionEffect condition, float seed, Texture2D? normalMap)
    {
        _shaderParameters["rarity_effect"] = (int)rarity;
        _shaderParameters["condition_effect"] = (int)condition;
        _shaderParameters["art_seed"] = seed;

        if (normalMap != null)
            _shaderParameters["art_normal_map"] = normalMap;
        else
            _shaderParameters.Remove("art_normal_map");
    }

    public ShaderMaterial? ApplyMaterial(MeshInstance3D target)
    {
        if (CardMaterialTemplate == null) return null;

        if (CardMaterialTemplate.Duplicate() is not ShaderMaterial material) return null;

        foreach (var param in _shaderParameters) material.SetShaderParameter(param.Key, param.Value);

        target.MaterialOverride = material;
        _activeMaterial = material;
        return material;
    }

    private static ImageTexture CreateEmptyLayer()
    {
        var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        return ImageTexture.CreateFromImage(image);
    }
}
