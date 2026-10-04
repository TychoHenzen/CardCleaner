using System.IO;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

/// <summary>
/// Loads the compiled terrain atlas texture from the resource system or, failing that, from disk.
/// </summary>
internal static class TerrainAtlasLoader
{
    internal static Texture2D? Load(string atlasPath)
    {
        if (ResourceLoader.Exists(atlasPath))
            return ResourceLoader.Load<Texture2D>(atlasPath);

        var absolutePath = ProjectSettings.GlobalizePath(atlasPath);
        if (!File.Exists(absolutePath))
            return null;

        var image = Image.LoadFromFile(absolutePath);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }
}
