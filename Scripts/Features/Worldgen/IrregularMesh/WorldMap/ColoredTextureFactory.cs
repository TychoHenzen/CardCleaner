using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// Creates solid-colour textures for placeholder sprites.
/// </summary>
internal static class ColoredTextureFactory
{
    internal static ImageTexture Create(Color color, int width, int height)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }
}
