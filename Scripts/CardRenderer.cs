using Godot;
using CardCleaner.Scripts.Interfaces;

namespace CardCleaner.Scripts;

[Tool]
public partial class CardRenderer : Node, ICardComponent
{
    // --- Atlas resolution ---
    [Export] public int TextureWidth = 1024;
    [Export] public int TextureHeight = 1024;

    // --- Input textures & regions (normalized 0–1 over front) ---
    [Export] public Texture2D FrontTemplate;
    [Export] public Texture2D BorderTexture;
    [Export] public Texture2D ArtTexture;
    [Export] public Texture2D SymbolTexture;
    [Export] public Texture2D AttrBoxTexture;

    [Export] public Vector4 BorderRegion;
    [Export] public Vector4 ArtRegion;
    [Export] public Vector4 SymbolRegion;
    [Export] public Vector4 AttrBoxRegion;

    // --- Back side ---
    [Export] public Texture2D BackTexture;

    // --- Text fields (front only) ---
    [Export] public Font TextFont;
    [Export] public string NameText = "";
    [Export] public Vector4 NameRegion;
    [Export] public string AttributesText = "";
    [Export] public Vector4 AttrTextRegion;

    // --- Material to receive baked texture ---
    [Export] public StandardMaterial3D CardMaterial;
    [Export] public PackedScene ViewportScene;

    private bool _baked;

    public void Setup(RigidBody3D cardRoot)
    {
        if (_baked || CardMaterial == null)
            return;

        // Defer assigning until OuterBox_Baked exists
        CallDeferred(nameof(DeferredAssign));
        _baked = true;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // no-op
    }

    private TextureRect PlaceLayer(Texture2D tex, Vector4 region)
    {
        if (tex == null) return null;
        return new TextureRect
        {
            Texture = tex,
            Position = new Vector2(region.X * TextureWidth, region.Y * TextureHeight),
            Size = new Vector2((region.Z - region.X) * TextureWidth,
                (region.W - region.Y) * TextureHeight),
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.FitHeight
        };
    }

    private Label MakeLabel(string text, Vector4 region)
    {
        var lbl = new Label
        {
            Text = text,
            Position = new Vector2(region.X * TextureWidth, region.Y * TextureHeight),
            Size = new Vector2((region.Z - region.X) * TextureWidth,
                (region.W - region.Y) * TextureHeight),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (TextFont != null)
            lbl.AddThemeFontOverride("font", TextFont);
        return lbl;
    }

    void BlitLayer(Image tex, Rect2I srcRect, Vector2I dstPos, bool alphaBlend, Image img)
    {
        if (tex == null) return;
        if (tex.GetFormat() != img.GetFormat())
        {
            GD.Print($"[Debug] Converting {tex.ResourceName} from {tex.GetFormat()} to {img.GetFormat()}");
            tex.Convert(img.GetFormat());
        }

        if (alphaBlend)
            img.BlendRect(tex, srcRect, dstPos);
        else
            img.BlitRect(tex, srcRect, dstPos);
    }

    private static Vector4 Remap(Vector4 r) => new(r.X * 0.5f, r.Y, r.Z * 0.5f, r.W);

    private ImageTexture GenerateCompositeTexture()
    {
        // 1) Create an empty RGBA image and fill with white
        var img = Image.CreateEmpty(TextureWidth, TextureHeight, false, Image.Format.Rgba8);
        img.Fill(new Color(1f, 0f, 0f, 1f));

        // 2) Blit the back half (opaque) into the right side
        var backImg = BackTexture.GetImage();
        BlitLayer(backImg,
            new Rect2I(Vector2I.Zero, backImg.GetSize()),
            new Vector2I(TextureWidth / 2, 0), true, img);

        // 3) Blit the front template into the left half
        var frontImg = FrontTemplate.GetImage();
        BlitLayer(frontImg,
            new Rect2I(Vector2I.Zero, frontImg?.GetSize() ?? Vector2I.Zero)
            , Vector2I.Zero, true, img);

        // 4) Blend the remaining layers with alpha
        var borderImg = BorderTexture?.GetImage();
        BlitLayer(borderImg, new Rect2I(Vector2I.Zero, borderImg?.GetSize() ?? Vector2I.Zero),
            new Vector2I((int)(BorderRegion.X * TextureWidth * 0.5f), (int)(BorderRegion.Y * TextureHeight)),true, img);
        
        var artImg = ArtTexture?.GetImage();
        BlitLayer(artImg, new Rect2I(Vector2I.Zero, artImg?.GetSize() ?? Vector2I.Zero),
            new Vector2I((int)(ArtRegion.X * TextureWidth * 0.5f), (int)(ArtRegion.Y * TextureHeight)),true, img);
        
        var symbolImg = SymbolTexture?.GetImage();
        BlitLayer(symbolImg, new Rect2I(Vector2I.Zero, symbolImg?.GetSize() ?? Vector2I.Zero),
            new Vector2I((int)(SymbolRegion.X * TextureWidth * 0.5f), (int)(SymbolRegion.Y * TextureHeight)),true, img);
        
        var attrBoxImg = AttrBoxTexture?.GetImage();
        BlitLayer(attrBoxImg, new Rect2I(Vector2I.Zero, attrBoxImg?.GetSize() ?? Vector2I.Zero),
            new Vector2I((int)(AttrBoxRegion.X * TextureWidth * 0.5f), (int)(AttrBoxRegion.Y * TextureHeight)),true, img);
        

        // 5) Pack into an ImageTexture and return
        var atlasTex = ImageTexture.CreateFromImage(img);
        atlasTex.SetImage(img);
        return atlasTex;
    }

    private void DeferredAssign()
    {
        // Look for the mesh OuterBox_Baked next to this node
        var box = GetParent().GetNodeOrNull<MeshInstance3D>("OuterBox_Baked");
        if (box == null)
        {
            // Try again next frame until CsgBaker creates it
            CallDeferred(nameof(DeferredAssign));
            return;
        }

        var vpTex = GenerateCompositeTexture();
        // Apply our material (with AlbedoTexture == _pendingTexture)
        if (CardMaterial.Duplicate() is not StandardMaterial3D instance) return;

        instance.AlbedoTexture = vpTex;
        box.MaterialOverride = instance;
        var nameLabel = MakeLabel(NameText, NameRegion);
        box.AddChild(nameLabel);
        var attrLabel = MakeLabel(AttributesText, AttrTextRegion);
        box.AddChild(attrLabel);
    }
}