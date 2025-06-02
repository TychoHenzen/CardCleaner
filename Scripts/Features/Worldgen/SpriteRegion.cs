using Godot;

[Tool]
[GlobalClass]
public partial class SpriteRegion : Resource
{
    private Texture2D? _sourceTexture;
    private Rect2I _sourceRect;
    
    [Export] 
    public Texture2D? SourceTexture 
    { 
        get => _sourceTexture;
        set
        {
            _sourceTexture = value;
            UpdatePreview();
        }
    }
    
    [Export] 
    public Rect2I SourceRect 
    { 
        get => _sourceRect;
        set
        {
            _sourceRect = value;
            UpdatePreview();
        }
    }
    
    [Export] public ImageTexture? PreviewTexture { get; private set; }
        
    public SpriteRegion()
    {
        // Default constructor for Godot
        UpdatePreview();
    }
        
    public SpriteRegion(Texture2D texture, Rect2I rect, string name = "")
    {
        _sourceTexture = texture;
        _sourceRect = rect;
        UpdatePreview();
    }
    
    private void UpdatePreview()
    {
        if (_sourceTexture == null)
        {
            PreviewTexture = null;
            return;
        }
            
        var sourceImage = _sourceTexture.GetImage();
        if (sourceImage == null)
        {
            PreviewTexture = null;
            return;
        }
            
        // Validate the source rect bounds
        var textureSize = _sourceTexture.GetSize();
        if (_sourceRect.Position.X < 0 || _sourceRect.Position.Y < 0 ||
            _sourceRect.End.X > textureSize.X || _sourceRect.End.Y > textureSize.Y ||
            _sourceRect.Size.X <= 0 || _sourceRect.Size.Y <= 0)
        {
            PreviewTexture = null;
            return;
        }
        // Create a cropped image from the source region
        var croppedImage = sourceImage.GetRegion(_sourceRect);
        
        // Validate the cropped image
        if (croppedImage == null || croppedImage.GetSize() == Vector2I.Zero)
        {
            PreviewTexture = null;
            return;
        }
        
        // Convert to RGBA8 format to ensure compatibility
        croppedImage.Convert(Image.Format.Rgba8);
        
        // Generate mipmaps for better display quality
        croppedImage.GenerateMipmaps();
        
        // Create and set the preview texture
        PreviewTexture = ImageTexture.CreateFromImage(croppedImage);

    }
}