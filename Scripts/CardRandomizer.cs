using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts;

public class CardRandomizer(RandomNumberGenerator rng, CardTemplate template) : ICardGenerator
{
    private static void VerifyTextures(Texture2D[] list)
    {
        foreach (var texture in list)
        {
            if (texture.GetImage().GetFormat() != Image.Format.Rgba8)
            {
                GD.PrintErr($"Image {texture.ResourcePath} does not have argb8");
            }
        }
    }
    public void Verify()
    {
        
#if TOOLS
        if (!Engine.IsEditorHint()) return;
        
        VerifyTextures(template.CardBaseOptions);
        VerifyTextures(template.BorderOptions);
        VerifyTextures(template.CornerOptions);
        VerifyTextures(template.ArtOptions);
        VerifyTextures(template.SymbolOptions);
        VerifyTextures(template.ImageBackgroundOptions);
        VerifyTextures(template.BannerOptions);
        VerifyTextures(template.DescriptionBoxOptions);
        VerifyTextures(template.EnergyContainerOptions);
        VerifyTextures(template.EnergyFill1Options);
        VerifyTextures(template.EnergyFill2Options);
        VerifyTextures(template.EnergyFillFullOptions);
        VerifyTextures(template.GemSocketsOptions);
        VerifyTextures(template.GemsOptions);
#endif
    }
    public void RandomizeCardRenderer(CardShaderRenderer renderer)
    {

        Randomize(renderer.CardBase, template.CardBaseOptions);
        Randomize(renderer.Border, template.BorderOptions);
        Randomize(renderer.Corners, template.CornerOptions);

        Randomize(renderer.Art, template.ArtOptions);
        Randomize(renderer.Symbol, template.SymbolOptions);
        Randomize(renderer.ImageBackground, template.ImageBackgroundOptions);
        Randomize(renderer.Banner, template.BannerOptions);
        Randomize(renderer.DescriptionBox, template.DescriptionBoxOptions);
        Randomize(renderer.EnergyContainer, template.EnergyContainerOptions);

        if (rng.Randf() > 0.5f)
        {
            Randomize(renderer.EnergyFill1, template.EnergyFill1Options);
            Randomize(renderer.EnergyFill2, template.EnergyFill2Options);
        }
        else
        {
            Randomize(renderer.EnergyFill1, template.EnergyFillFullOptions);
            Randomize(renderer.EnergyFill2, template.EnergyFillFullOptions);
        }

        for (int i = 0; i < 8; i++)
        {
            Randomize(renderer.GemSockets[i], template.GemSocketsOptions);
            Randomize(renderer.Gems[i], template.GemsOptions);
        }
    }
    
    private void Randomize(LayerData target, Texture2D[] textures)
    {
        if (textures.Length > 1)
            target.Texture = textures[rng.RandiRange(0, textures.Length - 1)];
    }

}