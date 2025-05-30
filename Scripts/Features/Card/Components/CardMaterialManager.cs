using System.Collections.Generic;
using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

[Tool]
public partial class CardMaterialManager : Node, ICardMaterialComponent
{
    [Export] public ShaderMaterial CardMaterialTemplate { get; set; }
    
    private ShaderMaterial _activeMaterial;
    private readonly Dictionary<string, Variant> _shaderParameters = new();

    
    public void SetLayerTextures(LayerData[] layers)
    {
        var texturesArr = new Godot.Collections.Array<Texture2D>();
        var regionsArr = new Godot.Collections.Array<Vector4>();
        var frontFlagsArr = new Godot.Collections.Array<bool>();
        var backFlagsArr = new Godot.Collections.Array<bool>();

        foreach (var layer in layers)
        {
            texturesArr.Add(layer.Texture);
            regionsArr.Add(layer.Region);
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
        if (!_shaderParameters.ContainsKey("gem_emission_colors"))
        {
            var colors = new Godot.Collections.Array<Vector3>();
            var strengths = new Godot.Collections.Array<float>();
        
            // Initialize with 8 empty slots
            for (int i = 0; i < 8; i++)
            {
                colors.Add(Vector3.Zero);
                strengths.Add(0.0f);
            }
        
            _shaderParameters["gem_emission_colors"] = colors;
            _shaderParameters["gem_emission_strengths"] = strengths;
        }
    
        var existingColors = _shaderParameters["gem_emission_colors"].As<Godot.Collections.Array<Vector3>>();
        var existingStrengths = _shaderParameters["gem_emission_strengths"].As<Godot.Collections.Array<float>>();
    
        if (index >= 0 && index < 8)
        {
            existingColors[index] = new Vector3(color.R, color.G, color.B);
            existingStrengths[index] = strength;
        }
    }

    public void ApplyMaterial(MeshInstance3D target)
    {
        if (CardMaterialTemplate?.Duplicate() is not ShaderMaterial material) return;

        foreach (var param in _shaderParameters)
        {
            material.SetShaderParameter(param.Key, param.Value);
        }

        target.MaterialOverride = material;
        _activeMaterial = material;
    }
}