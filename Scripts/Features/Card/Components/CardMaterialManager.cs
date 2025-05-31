using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Card.Components;

[Tool]
public partial class CardMaterialManager : Node, ICardMaterialComponent
{
    private readonly Dictionary<string, Variant> _shaderParameters = new();

    private ShaderMaterial _activeMaterial;
    [Export] public ShaderMaterial CardMaterialTemplate { get; set; }

    public void SetLayerTextures(Core.Data.LayerData[] layers)
    {
        var texturesArr = new Array<Texture2D>();
        var regionsArr = new Array<Vector4>();
        var frontFlagsArr = new Array<bool>();
        var backFlagsArr = new Array<bool>();

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
    }public void SetGemEmission(int index, Color color, float strength)
    {
        // Ensure arrays exist with proper size
        if (!_shaderParameters.ContainsKey("gem_emission_colors"))
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
        }

        var existingColors = _shaderParameters["gem_emission_colors"].As<Array<Vector3>>();
        var existingStrengths = _shaderParameters["gem_emission_strengths"].As<Array<float>>();

        if (index is < 0 or >= 8) 
        {
            GD.PrintErr($"[CardMaterialManager] Invalid gem index: {index}");
            return;
        }
    
        existingColors[index] = new Vector3(color.R, color.G, color.B);
        existingStrengths[index] = strength;
    }

    public void ApplyMaterial(MeshInstance3D target)
    {
        if (CardMaterialTemplate?.Duplicate() is not ShaderMaterial material) return;
        
        foreach (var param in _shaderParameters) 
        {
            material.SetShaderParameter(param.Key, param.Value);
        
            // Debug emission arrays specifically
            if (param.Key == "gem_emission_colors" || param.Key == "gem_emission_strengths")
            {
                GD.Print($"[CardMaterialManager] Applied {param.Key}: {param.Value}");
            }
        }

        target.MaterialOverride = material;
        _activeMaterial = material;
    }
}