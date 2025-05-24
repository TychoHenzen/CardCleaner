using Godot;
using System;

public partial class CardVisual : Node3D
{
    // Drag-and-drop these in the Inspector
    [Export] public ShaderMaterial CardMaterial { get; set; }
    [Export] public Texture2D BackTexture   { get; set; }
    [Export] public Texture2D FrontTexture  { get; set; }
    [Export] public Texture2D BorderTexture { get; set; }
    [Export] public Texture2D ArtTexture    { get; set; }
    [Export] public Texture2D SymbolTexture { get; set; }
    [Export] public Texture2D NameTexture   { get; set; }
    [Export] public Texture2D AttrBoxTexture{ get; set; }
    [Export] public Texture2D AttrTextTex   { get; set; }
    [Export] public Texture2D EmissiveMask  { get; set; }

    // These region vectors you’ll calculate to match your UV layout:
    [Export] public Vector4 BorderRegion;
    [Export] public Vector4 ArtRegion;
    [Export] public Vector4 SymbolRegion;
    [Export] public Vector4 NameRegion;
    [Export] public Vector4 AttrRegion;
    [Export] public Vector4 AttrTextRegion;

    public override void _Ready()
    {
        // Assign all the textures
        CardMaterial.SetShaderParameter("back_texture",        BackTexture);
        CardMaterial.SetShaderParameter("front_texture",       FrontTexture);
        CardMaterial.SetShaderParameter("border_texture",      BorderTexture);
        CardMaterial.SetShaderParameter("art_texture",         ArtTexture);
        CardMaterial.SetShaderParameter("symbol_texture",      SymbolTexture);
        CardMaterial.SetShaderParameter("name_texture",        NameTexture);
        CardMaterial.SetShaderParameter("attribute_box_texture", AttrBoxTexture);
        CardMaterial.SetShaderParameter("attribute_text_texture", AttrTextTex);
        CardMaterial.SetShaderParameter("emissive_mask",       EmissiveMask);

        // Assign all the UV‐regions
        CardMaterial.SetShaderParameter("border_region",    BorderRegion);
        CardMaterial.SetShaderParameter("art_region",       ArtRegion);
        CardMaterial.SetShaderParameter("symbol_region",    SymbolRegion);
        CardMaterial.SetShaderParameter("name_region",      NameRegion);
        CardMaterial.SetShaderParameter("attribute_region", AttrRegion);
        CardMaterial.SetShaderParameter("attribute_text_region", AttrTextRegion);

        // Start blacklight off
        CardMaterial.SetShaderParameter("blacklight_enabled", 0.0f);
    }

    /// <summary>
    /// Toggle the blacklight emissive on or off at runtime.
    /// </summary>
    public void SetBlacklight(bool enabled)
    {
        CardMaterial.SetShaderParameter("blacklight_enabled",
            enabled ? 1.0f : 0.0f);
    }

    /// <summary>
    /// Example API: reconfigure the card from a data struct.
    /// </summary>
    public void ApplyCardData(CardData data)
    {
        CardMaterial.SetShaderParameter("front_texture",       data.Front);
        CardMaterial.SetShaderParameter("art_texture",         data.Art);
        CardMaterial.SetShaderParameter("name_texture",        data.NameImage);
        CardMaterial.SetShaderParameter("attribute_text_texture", data.AttrsImage);
        // …and so on for regions or any other variants…
    }
}

// A simple holder for your card’s data