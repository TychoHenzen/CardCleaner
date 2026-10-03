#if TOOLS
using System;
using System.IO;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewControl
{
    private Texture2D? GetAutoTileTexture()
    {
        if (_autoTileDef == null || _currentTilesetRef == null)
            return null;

        return GetTextureForTileset(_currentTilesetRef);
    }

    private Texture2D? GetTextureForTileset(TmxTilesetReference tilesetRef)
    {
        var absoluteTexturePath = tilesetRef.TilesetData.TilesetPath;
        GD.Print(
            $"[TmxPreviewControl] GetTextureForTileset: "
            + $"TilesetPath='{absoluteTexturePath}'");
        if (string.IsNullOrEmpty(absoluteTexturePath))
        {
            GD.PrintErr("[TmxPreviewControl] TilesetPath is empty!");
            return null;
        }

        if (_textureCache.TryGetValue(absoluteTexturePath, out var cached))
            return cached;

        Texture2D? texture = null;
        try
        {
            GD.Print(
                $"[TmxPreviewControl] Checking if file exists: "
                + absoluteTexturePath);
            if (File.Exists(absoluteTexturePath))
            {
                GD.Print("[TmxPreviewControl] File exists, loading image...");
                var image = Image.LoadFromFile(absoluteTexturePath);
                if (image != null)
                {
                    texture = ImageTexture.CreateFromImage(image);
                    GD.Print(
                        $"[TmxPreviewControl] Texture loaded successfully: "
                        + $"{texture.GetWidth()}x{texture.GetHeight()}");
                }
                else
                {
                    GD.PrintErr(
                        "[TmxPreviewControl] Image.LoadFromFile returned null");
                }
            }
            else
            {
                GD.PrintErr(
                    $"[TmxPreviewControl] Texture file not found: "
                    + absoluteTexturePath);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr(
                $"[TmxPreviewControl] Failed to load texture: {ex.Message}\n"
                + ex.StackTrace);
        }

        _textureCache[absoluteTexturePath] = texture;
        return texture;
    }
}
#endif
