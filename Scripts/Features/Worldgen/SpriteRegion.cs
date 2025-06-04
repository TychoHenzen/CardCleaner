using CardCleaner.Scripts.Core.Data;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class SpriteRegion : Resource
{
    [Export] public TileSet? TileSet { get; set; }
    [Export] public TileReference[] Layers { get; set; }

    private bool _updatePreview;

    [Export]
    public bool UpdatePreview
    {
        get => _updatePreview;
        set
        {
            if (!value)
                return;
            UpdatePreviewInternal();
            _updatePreview = false;
            NotifyPropertyListChanged();
        }
    }

    [Export] public ImageTexture? PreviewTexture { get; private set; }

    public SpriteRegion()
    {
        UpdatePreviewInternal();
    }

    public SpriteRegion(TileReference[]? tileReferences)
    {
        Layers = tileReferences ?? System.Array.Empty<TileReference>();
        UpdatePreviewInternal();
    }

    public SpriteRegion(TileSet tileSet, Vector2I atlasCoords)
    {
        Layers = new[]
        {
            new TileReference
            {
                AtlasCoords = atlasCoords,
            }
        };
        UpdatePreviewInternal();
    }

    private void UpdatePreviewInternal()
    {
        if (Layers == null || Layers.Length == 0)
        {
            PreviewTexture = null;
            return;
        }

        // Get base size from first valid tile
        Vector2I baseSize = Vector2I.Zero;
        foreach (var tileRef in Layers)
        {
            var textureRegion = GetTextureRegion(tileRef);
            if (textureRegion is not { Texture: not null })
                continue;
            baseSize = new Vector2I((int)textureRegion.Region.Size.X, (int)textureRegion.Region.Size.Y);
            break;
        }

        if (baseSize.X <= 0 || baseSize.Y <= 0)
        {
            PreviewTexture = null;
            return;
        }

        // Create single tile composite
        var singleTileImage = CreateCompositeImage(baseSize);
        if (singleTileImage == null)
        {
            PreviewTexture = null;
            return;
        }

        // Create 3x3 grid from the single tile
        var gridSize = new Vector2I(baseSize.X * 3, baseSize.Y * 3);
        var gridImage = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);
        gridImage.Fill(Colors.Transparent);

        // Tile the single image 9 times in a 3x3 grid
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var destPosition = new Vector2I(col * baseSize.X, row * baseSize.Y);
                gridImage.BlitRect(singleTileImage, 
                    new Rect2I(Vector2I.Zero, baseSize), 
                    destPosition);
            }
        }

        gridImage.GenerateMipmaps();
        PreviewTexture = ImageTexture.CreateFromImage(gridImage);
    }

    private Image? CreateCompositeImage(Vector2I baseSize)
    {
        var compositeImage = Image.CreateEmpty(baseSize.X, baseSize.Y, false, Image.Format.Rgba8);
        compositeImage.Fill(Colors.Transparent);

        // Blend each tile reference in order
        foreach (var tileRef in Layers)
        {
            var data = GetTextureRegion(tileRef);
            if (data == null) continue;

            var tileImage = data.Texture?.GetImage();
            if (tileImage == null) continue;

            var regionRect = new Rect2I((int)data.Region.Position.X,
                (int)data.Region.Position.Y,
                (int)data.Region.Size.X,
                (int)data.Region.Size.Y);

            if (regionRect.Size.X <= 0 || regionRect.Size.Y <= 0) continue;

            var croppedTile = tileImage.GetRegion(regionRect);
            if (croppedTile == null) continue;

            croppedTile.Convert(Image.Format.Rgba8);
            compositeImage.BlendRect(croppedTile, new Rect2I(Vector2I.Zero, croppedTile.GetSize()), Vector2I.Zero);
        }

        return compositeImage;
    }

    private LayerData? GetTextureRegion(TileReference tileRef)
    {
        var source = TileSet?.GetSource(tileRef.SourceId);
        if (source is not TileSetAtlasSource atlasSource)
            return null;

        var texture = atlasSource.Texture;
        if (texture == null)
            return null;

        var region = atlasSource.GetTileTextureRegion(tileRef.AtlasCoords);
        return new LayerData { Texture = texture, Region = region };
    }
}