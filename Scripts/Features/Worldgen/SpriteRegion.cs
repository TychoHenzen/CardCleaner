using Godot;
using CardCleaner.Scripts.Core.Data;

[Tool]
[GlobalClass]
public partial class SpriteRegion : Resource
{
    private LayerData[] _layers = System.Array.Empty<LayerData>();
    
    [Export] 
    public LayerData[] Layers 
    { 
        get => _layers;
        set
        {
            _layers = value ?? System.Array.Empty<LayerData>();
            UpdatePreview();
        }
    }
    
    [Export] public ImageTexture? PreviewTexture { get; private set; }
        
    public SpriteRegion()
    {
        UpdatePreview();
    }
        
    public SpriteRegion(LayerData[] layers)
    {
        _layers = layers ?? System.Array.Empty<LayerData>();
        UpdatePreview();
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
            _layers = new[] { layerData };
        }
        else
        {
            _layers = System.Array.Empty<LayerData>();
        }
        UpdatePreview();
    }

    
    private void UpdatePreview()
    {
        if (_layers == null || _layers.Length == 0)
        {
            PreviewTexture = null;
            return;
        }
        
        // Find the base layer size (first layer with valid texture)
        Vector2I baseSize = Vector2I.Zero;
        foreach (var layer in _layers)
        {
            if (layer?.Texture != null)
            {
                var layerSize = layer.Texture.GetSize();
                var region = new Rect2I(
                    (int)(layer.Region.Position.X * layerSize.X),
                    (int)(layer.Region.Position.Y * layerSize.Y),
                    (int)(layer.Region.Size.X * layerSize.X),
                    (int)(layer.Region.Size.Y * layerSize.Y)
                );
                baseSize = region.Size;
                break;
            }
        }
        
        if (baseSize == Vector2I.Zero)
        {
            PreviewTexture = null;
            return;
        }
        
        // Create composite image
        var compositeImage = Image.CreateEmpty(baseSize.X, baseSize.Y, false, Image.Format.Rgba8);
        compositeImage.Fill(Colors.Transparent);
        
        // Blend each layer in order
        foreach (var layer in _layers)
        {
            var layerImage = layer?.Texture?.GetImage();
            if (layerImage == null) continue;
            
            var layerSize = layer.Texture.GetSize();
            var region = new Rect2I(
                (int)(layer.Region.Position.X * layerSize.X),
                (int)(layer.Region.Position.Y * layerSize.Y),
                (int)(layer.Region.Size.X * layerSize.X),
                (int)(layer.Region.Size.Y * layerSize.Y)
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