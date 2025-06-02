using Godot;

namespace CardCleaner.Scripts.Core.Data;

[Tool]
[GlobalClass]
public partial class LayerData : Resource
{
    [Export] public Texture2D? Texture { get; set; }
    [Export] public Rect2 Region { get; set; } = new(0, 0, 1, 1);
    [Export] public bool RenderOnFront { get; set; } = true;
    [Export] public bool RenderOnBack { get; set; }
}