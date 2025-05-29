using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts;

public class CardRandomizer : ICardGenerator
{
    private readonly CardTemplate _template;
    private readonly RandomNumberGenerator _rng;

    public CardRandomizer(RandomNumberGenerator rng, CardTemplate template)
    {
        _rng = rng;
        _template = template;
    }
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
        
        VerifyTextures(_template.CardBaseOptions);
        VerifyTextures(_template.BorderOptions);
        VerifyTextures(_template.CornerOptions);
        VerifyTextures(_template.ArtOptions);
        VerifyTextures(_template.SymbolOptions);
        VerifyTextures(_template.ImageBackgroundOptions);
        VerifyTextures(_template.BannerOptions);
        VerifyTextures(_template.DescriptionBoxOptions);
        VerifyTextures(_template.EnergyContainerOptions);
        VerifyTextures(_template.EnergyFill1Options);
        VerifyTextures(_template.EnergyFill2Options);
        VerifyTextures(_template.EnergyFillFullOptions);
        VerifyTextures(_template.GemSocketsOptions);
        VerifyTextures(_template.GemsOptions);
#endif
    }
    public void GenerateCardRenderer(CardShaderRenderer renderer, CardSignature signature)
    {

        Randomize(renderer.CardBase, _template.CardBaseOptions);
        Randomize(renderer.Border, _template.BorderOptions);
        Randomize(renderer.Corners, _template.CornerOptions);

        Randomize(renderer.Art, _template.ArtOptions);
        Randomize(renderer.Symbol, _template.SymbolOptions);
        Randomize(renderer.ImageBackground, _template.ImageBackgroundOptions);
        Randomize(renderer.Banner, _template.BannerOptions);
        Randomize(renderer.DescriptionBox, _template.DescriptionBoxOptions);
        Randomize(renderer.EnergyContainer, _template.EnergyContainerOptions);

        if (_rng.Randf() > 0.5f)
        {
            Randomize(renderer.EnergyFill1, _template.EnergyFill1Options);
            Randomize(renderer.EnergyFill2, _template.EnergyFill2Options);
        }
        else
        {
            Randomize(renderer.EnergyFill1, _template.EnergyFillFullOptions);
            Randomize(renderer.EnergyFill2, _template.EnergyFillFullOptions);
        }

        for (int i = 0; i < 8; i++)
        {
            Randomize(renderer.GemSockets[i], _template.GemSocketsOptions);
            Randomize(renderer.Gems[i], _template.GemsOptions);
        }
    }
    
    private void Randomize(LayerData target, Texture2D[] textures)
    {
        if (textures.Length > 1)
            target.Texture = textures[_rng.RandiRange(0, textures.Length - 1)];
    }

}