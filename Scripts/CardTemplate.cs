using System;
using Godot;

namespace CardCleaner.Scripts;
[System.Serializable]
[GlobalClass]
public partial class CardTemplate : Resource
{
    [Export] public Texture2D[] CardBaseOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] BorderOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] CornerOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] ArtOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] SymbolOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] ImageBackgroundOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] BannerOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] DescriptionBoxOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] EnergyContainerOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] EnergyFill1Options { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] EnergyFill2Options { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] EnergyFillFullOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] GemSocketsOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] GemsOptions { get; set; } = new Texture2D[0];
    
}