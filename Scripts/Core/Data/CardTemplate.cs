using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Core.Data;

public partial class CardTemplate : Resource
{
    // --- Front & back base templates ---
    public LayerData CardBase { get; } = new() { RenderOnFront = true, RenderOnBack = true };
    public LayerData Border { get; } = new() { RenderOnFront = true, RenderOnBack = true };
    public LayerData Corners { get; } = new() { RenderOnFront = true, RenderOnBack = true };

    // --- LayerData for paired texture+region+side ---
    public LayerData Art { get; } =
        new() { RenderOnFront = true, Region = new Rect2(0.1f, 0.1f, 0.8f, 0.35f) };

    public LayerData Symbol { get; } = new()
        { RenderOnBack = true, RenderOnFront = false, Region = new Rect2(0.2f, 0.2f, 0.6f, 0.6f) };

    public LayerData ImageBackground { get; } =
        new() { RenderOnFront = true, Region = new Rect2(0, 0, 1f, 0.5f) };

    public LayerData Banner { get; } = new()
        { RenderOnFront = true, Region = new Rect2(0.1f, 0.43f, 0.8f, 0.2f) };

    public LayerData DescriptionBox { get; } = new()
        { RenderOnFront = true, Region = new Rect2(0.015f, 0.43f, 1.01f, 0.59f) };

    public LayerData EnergyContainer { get; } =
        new() { RenderOnFront = true, Region = new Rect2(0.8f, 0, 0.2f, 0.15f) };

    public LayerData EnergyFill1 { get; } =
        new() { RenderOnFront = true, Region = new Rect2(0.8f, 0, 0.2f, 0.15f) };

    public LayerData EnergyFill2 { get; } =
        new() { RenderOnFront = true, Region = new Rect2(0.8f, 0, 0.2f, 0.15f) };

    public LayerData[] GemSockets { get; } = CreateGemLayers();

    public LayerData[] Gems { get; } = CreateGemLayers();

    private static LayerData[] CreateGemLayers()
    {
        return new[]
        {
            new LayerData { RenderOnFront = true, Region = new Rect2(0.9f, 0.25f, 0.1f, 0.075f) },
            new LayerData { RenderOnFront = true, Region = new Rect2(0.9f, 0.4f, 0.1f, 0.075f) },
            new LayerData { RenderOnFront = true, Region = new Rect2(0.9f, 0.55f, 0.1f, 0.075f) },
            new LayerData { RenderOnFront = true, Region = new Rect2(0.9f, 0.7f, 0.1f, 0.075f) },
            new LayerData { RenderOnFront = true, Region = new Rect2(0.0f, 0.25f, 0.1f, 0.075f) },
            new LayerData { RenderOnFront = true, Region = new Rect2(0.0f, 0.4f, 0.1f, 0.075f) },
            new LayerData { RenderOnFront = true, Region = new Rect2(0.0f, 0.55f, 0.1f, 0.075f) },
            new LayerData { RenderOnFront = true, Region = new Rect2(0.0f, 0.7f, 0.1f, 0.075f) }
        };
    }

    public LayerData[] GatherAllLayers()
    {
        return new[]
            {
                CardBase, Border, Corners, ImageBackground, DescriptionBox, Art, Banner, Symbol, EnergyFill1,
                EnergyFill2, EnergyContainer
            }
            .Concat(GemSockets)
            .Concat(Gems)
            .Reverse()
            .ToArray();
    }
}
