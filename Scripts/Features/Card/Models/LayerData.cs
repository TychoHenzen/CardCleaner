using Godot;

namespace CardCleaner.Scripts.Features.Card.Models;

[Tool]
[GlobalClass]
public partial class LayerData : Resource
{
    // Default values as constants
    private static readonly Rect2 DefaultRegion = new(0, 0, 1, 1);
    private const bool DefaultRenderOnFront = true;
    private const bool DefaultRenderOnBack = false;

    [Export] public Texture2D? Texture { get; set; }
    [Export] public Rect2 Region { get; set; } = DefaultRegion;
    [Export] public bool RenderOnFront { get; set; } = DefaultRenderOnFront;
    [Export] public bool RenderOnBack { get; set; } = DefaultRenderOnBack;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Region) => true,
            nameof(RenderOnFront) => true,
            nameof(RenderOnBack) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Region) => Variant.From(DefaultRegion),
            nameof(RenderOnFront) => DefaultRenderOnFront,
            nameof(RenderOnBack) => DefaultRenderOnBack,
            _ => base._PropertyGetRevert(property)
        };
    }
}