using System;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts;
public partial class CardTemplate : Resource
{
    
    // --- Front & back base templates ---
    public LayerData CardBase { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };
    public LayerData Border { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };
    public LayerData Corners { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };

    // --- LayerData for paired texture+region+side ---
    public LayerData Art { get; set; } =
        new() { RenderOnFront = true, Region = new Vector4(0.1f, 0.1f, 0.8f, 0.35f) };

    public LayerData Symbol { get; set; } = new()
        { RenderOnBack = true, RenderOnFront = false, Region = new Vector4(0.2f, 0.2f, 0.6f, 0.6f) };

    public LayerData ImageBackground { get; set; } =
        new() { RenderOnFront = true, Region = new Vector4(0, 0, 1f, 0.5f) };

    public LayerData Banner { get; set; } = new()
        { RenderOnFront = true, Region = new Vector4(0.1f, 0.43f, 0.8f, 0.2f) };

    public LayerData DescriptionBox { get; set; } = new()
        { RenderOnFront = true, Region = new Vector4(0.015f, 0.43f, 1.01f, 0.59f) };

    public LayerData EnergyContainer { get; set; } =
        new() { RenderOnFront = true, Region = new Vector4(0.8f, 0, 0.2f, 0.15f) };

    public LayerData EnergyFill1 { get; set; } =
        new() { RenderOnFront = true, Region = new Vector4(0.8f, 0, 0.2f, 0.15f) };

    public LayerData EnergyFill2 { get; set; } =
        new() { RenderOnFront = true, Region = new Vector4(0.8f, 0, 0.2f, 0.15f) };

    public LayerData[] GemSockets { get; set; } =
    {
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.25f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.4f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.55f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.7f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.25f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.4f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.55f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.7f, 0.1f, 0.075f) },
    };

    public LayerData[] Gems { get; set; } =
    {
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.25f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.4f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.55f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.7f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.25f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.4f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.55f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.7f, 0.1f, 0.075f) },
    };
    
    public LayerData[] GatherAllLayers()
    {
        return new[] { CardBase, Border, Corners, ImageBackground, DescriptionBox, Art, Banner, Symbol, EnergyFill1, EnergyFill2, EnergyContainer }
            .Concat(GemSockets)
            .Concat(Gems)
            .Reverse()
            .ToArray();
    }
}