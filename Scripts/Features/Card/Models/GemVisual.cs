using CardCleaner.Scripts.Core.Enum;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Models;

[Tool]
[GlobalClass]
public partial class GemVisual : Resource
{
    [Export] public Element Element { get; set; } = Element.Solidum;

    // Textures for the positive aspect
    [Export] public Texture2D SocketTexture { get; set; }
    [Export] public Texture2D PositiveGemTexture { get; set; }
    [Export] public Color PositiveEmissionColor { get; set; } = new(1f, 1f, 1f);

    [Export(PropertyHint.Range, "0.0,10.0,0.1")]
    public float PositiveEmissionStrength { get; set; } = 1.0f;

// Textures for the negative aspect
    [Export] public Texture2D NegativeGemTexture { get; set; }
    [Export] public Color NegativeEmissionColor { get; set; } = new(1f, 1f, 1f);

    [Export(PropertyHint.Range, "0.0,10.0,0.1")]
    public float NegativeEmissionStrength { get; set; } = 1.0f;
}