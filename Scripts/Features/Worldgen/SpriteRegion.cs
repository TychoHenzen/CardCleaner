using Godot;
using CardCleaner.Scripts.Core.Data;

[Tool]
[GlobalClass]
public partial class SpriteRegion : Resource
{
    [Export] public LayerData[]? Layers { get; set; }

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

    public SpriteRegion(LayerData[]? layers)
    {
        Layers = layers ?? System.Array.Empty<LayerData>();
        UpdatePreviewInternal();
    }

    public SpriteRegion(Texture2D? texture, Rect2I rect)
    {
        if (texture != null)
        {
            var layerData = new LayerData
            {
                Texture = texture,
                Region = new Rect2I(
                    rect.Position.X / texture.GetWidth(),
                    rect.Position.Y / texture.GetHeight(),
                    rect.Size.X / texture.GetWidth(),
                    rect.Size.Y / texture.GetHeight()
                )
            };
            Layers = new[] { layerData };
        }
        else
        {
            Layers = System.Array.Empty<LayerData>();
        }

        UpdatePreviewInternal();
    }


    private void UpdatePreviewInternal()
    {
        if (Layers == null || Layers.Length == 0)
        {
            PreviewTexture = null;
            return;
        }

        // Find the base layer size (first layer with valid texture)
        Vector2I baseSize = Vector2I.Zero;
        foreach (var layer in Layers)
        {
            if (layer.Texture == null)
                continue;
            var region = new Rect2I(
                (int)layer.Region.Position.X,
                (int)layer.Region.Position.Y,
                (int)layer.Region.Size.X,
                (int)layer.Region.Size.Y
            );
            baseSize = region.Size;
            break;
        }

        // Validate baseSize has positive dimensions
        if (baseSize.X <= 0 || baseSize.Y <= 0)
        {
            PreviewTexture = null;
            return;
        }

        // Create composite image
        var compositeImage = Image.CreateEmpty(baseSize.X, baseSize.Y, false, Image.Format.Rgba8);
        compositeImage.Fill(Colors.Transparent);

        // Blend each layer in order
        foreach (var layer in Layers)
        {
            var layerImage = layer.Texture?.GetImage();
            if (layerImage == null) continue;

            var region = new Rect2I(
                (int)layer.Region.Position.X,
                (int)layer.Region.Position.Y,
                (int)layer.Region.Size.X,
                (int)layer.Region.Size.Y
            );

            if (region.Size.X <= 0 || region.Size.Y <= 0) continue;

            var croppedLayer = layerImage.GetRegion(region);
            if (croppedLayer == null) continue;

            croppedLayer.Convert(Image.Format.Rgba8);
            compositeImage.BlendRect(croppedLayer, new Rect2I(Vector2I.Zero, croppedLayer.GetSize()), Vector2I.Zero);
        }

        compositeImage.GenerateMipmaps();
        PreviewTexture = ImageTexture.CreateFromImage(compositeImage);
    }
}