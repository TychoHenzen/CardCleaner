using CardCleaner.Scripts;
using Godot;

[Tool]
[GlobalClass]
public partial class RarityVisual : Resource
{
    [Export] public CardRarity Rarity { get; set; } = CardRarity.Common;
    [Export] public Texture2D[] BaseOptions { get; set; } = { };
    [Export] public Texture2D[] BorderOptions { get; set; } = { };
    [Export] public Texture2D[] CornerOptions { get; set; } = { };
    [Export] public Texture2D[] BannerOptions { get; set; } = { };
    [Export] public Texture2D[] ImageBackgroundOptions { get; set; } = { };
    [Export] public Texture2D[] DescriptionBoxOptions { get; set; } = { };
    [Export] public Texture2D[] EnergyContainerOptions { get; set; } = { };
}