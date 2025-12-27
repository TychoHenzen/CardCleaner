using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Models;

[Tool]
[GlobalClass]
public partial class GemVisual : Resource
{
    // Default values as constants
    private const Element DefaultElement = Element.Solidum;
    private static readonly Color DefaultPositiveEmissionColor = new(1f, 1f, 1f);
    private const float DefaultPositiveEmissionStrength = 1.0f;
    private static readonly Color DefaultNegativeEmissionColor = new(1f, 1f, 1f);
    private const float DefaultNegativeEmissionStrength = 1.0f;

    [Export] public Element Element { get; set; } = DefaultElement;

    // Textures for the positive aspect
    [Export] public Texture2D SocketTexture { get; set; } = null!;
    [Export] public Texture2D PositiveGemTexture { get; set; } = null!;
    [Export] public Color PositiveEmissionColor { get; set; } = DefaultPositiveEmissionColor;

    [Export(PropertyHint.Range, "0.0,10.0,0.1")]
    public float PositiveEmissionStrength { get; set; } = DefaultPositiveEmissionStrength;

    // Textures for the negative aspect
    [Export] public Texture2D NegativeGemTexture { get; set; } = null!;
    [Export] public Color NegativeEmissionColor { get; set; } = DefaultNegativeEmissionColor;

    [Export(PropertyHint.Range, "0.0,10.0,0.1")]
    public float NegativeEmissionStrength { get; set; } = DefaultNegativeEmissionStrength;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Element) => true,
            nameof(PositiveEmissionColor) => true,
            nameof(PositiveEmissionStrength) => true,
            nameof(NegativeEmissionColor) => true,
            nameof(NegativeEmissionStrength) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Element) => (int)DefaultElement,
            nameof(PositiveEmissionColor) => Variant.From(DefaultPositiveEmissionColor),
            nameof(PositiveEmissionStrength) => DefaultPositiveEmissionStrength,
            nameof(NegativeEmissionColor) => Variant.From(DefaultNegativeEmissionColor),
            nameof(NegativeEmissionStrength) => DefaultNegativeEmissionStrength,
            _ => base._PropertyGetRevert(property)
        };
    }
}