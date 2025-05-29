using CardCleaner.Scripts;
using Godot;

[Tool]
[GlobalClass]
public partial class GemVisual : Resource
{
    [Export] public Aspect Aspect { get; set; } = Aspect.Ignis;
    [Export] public Texture2D GemTexture { get; set; }
    [Export] public Texture2D SocketTexture { get; set; }
}