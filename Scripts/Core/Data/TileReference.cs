using Godot;

namespace CardCleaner.Scripts.Core.Data;

[Tool]
[GlobalClass]
public partial class TileReference : Resource
{
    [Export] public int SourceId { get; set; } = 0;
    [Export] public Vector2I AtlasCoords { get; set; } = Vector2I.Zero;
}