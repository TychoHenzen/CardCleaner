using CardCleaner.Scripts.Core.Enumeration;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Models;

[Tool]
[GlobalClass]
public partial class RarityVisual : Resource
{
    // Default values as constants
    private const CardRarity DefaultRarity = CardRarity.Common;

    [Export] public CardRarity Rarity { get; set; } = DefaultRarity;
    [Export] public Texture2D[] BaseOptions { get; set; } = [];
    [Export] public Texture2D[] BorderOptions { get; set; } = [];
    [Export] public Texture2D[] CornerOptions { get; set; } = [];
    [Export] public Texture2D[] BannerOptions { get; set; } = [];
    [Export] public Texture2D[] ImageBackgroundOptions { get; set; } = [];
    [Export] public Texture2D[] DescriptionBoxOptions { get; set; } = [];
    [Export] public Texture2D[] EnergyContainerOptions { get; set; } = [];

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Rarity) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Rarity) => (int)DefaultRarity,
            _ => base._PropertyGetRevert(property)
        };
    }
}