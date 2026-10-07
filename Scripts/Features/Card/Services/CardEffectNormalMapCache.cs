using System;
using System.Runtime.CompilerServices;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Bakes a card's art into its bevel map once and hands the same <see cref="ImageTexture" /> back for the same
///     art texture afterwards. The cache entry lives exactly as long as the art texture does.
/// </summary>
public static class CardEffectNormalMapCache
{
    private static readonly ConditionalWeakTable<Texture2D, ImageTexture> Baked = new();

    public static ImageTexture GetOrBake(Texture2D art)
    {
        return Baked.GetValue(art, Bake);
    }

    private static ImageTexture Bake(Texture2D art)
    {
        var image = art.GetImage() ?? throw new InvalidOperationException($"Art texture {art.ResourcePath} has no image to bake.");
        if (image.IsCompressed())
            image.Decompress();

        // GetData holds every mip level of an imported texture; the baker wants the base level only.
        image.ClearMipmaps();
        image.Convert(Image.Format.Rgba8);
        var map = CardEffectNormalBaker.Bake(image.GetData(), image.GetWidth(), image.GetHeight());
        return ImageTexture.CreateFromImage(Image.CreateFromData(map.Width, map.Height, false, Image.Format.Rgba8, map.Rgba));
    }
}
