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
        img.Fill(new Color(1f, 0f, 0f, 0f));

        // 2) Blit the back half (opaque) into the right side
        var backImg = BackTexture.GetImage();
        backImg.Resize(TextureWidth / 2, TextureHeight, Image.Interpolation.Nearest);
        BlitLayer(backImg,
            new Rect2I(Vector2I.Zero, backImg.GetSize()),
            new Vector2I(TextureWidth / 2, 0), true, img);

        // 3) Blit the front template into the left half
        var frontImg = FrontTemplate.GetImage();
        frontImg.Resize(TextureWidth / 2, TextureHeight, Image.Interpolation.Nearest);
        BlitLayer(frontImg,
            new Rect2I(Vector2I.Zero, frontImg.GetSize())
            , Vector2I.Zero, true, img);

        // 4) Blend the remaining layers with alpha
        if (BorderTexture != null)
        {
            var borderImg = BorderTexture.GetImage();
            int bw = (int)((BorderRegion.Z - BorderRegion.X) * TextureWidth * 0.5f);
            int bh = (int)((BorderRegion.W - BorderRegion.Y) * TextureHeight);
            borderImg.Resize(bw, bh, Image.Interpolation.Nearest);
            int bx = (int)(BorderRegion.X * TextureWidth * 0.5f);
            int by = (int)(BorderRegion.Y * TextureHeight);
            BlitLayer(borderImg,
                new Rect2I(Vector2I.Zero, borderImg.GetSize()),
                new Vector2I(bx, by), true, img);
        }
        
        if (ArtTexture != null)
        {
            var artImg = ArtTexture.GetImage();
            int aw = (int)((ArtRegion.Z - ArtRegion.X) * TextureWidth * 0.5f);
            int ah = (int)((ArtRegion.W - ArtRegion.Y) * TextureHeight);
            artImg.Resize(aw, ah, Image.Interpolation.Nearest);
            int ax = (int)(ArtRegion.X * TextureWidth * 0.5f);
            int ay = (int)(ArtRegion.Y * TextureHeight);
            BlitLayer(artImg,
                new Rect2I(Vector2I.Zero, artImg.GetSize()),
                new Vector2I(ax, ay), true, img);
        }
        
        if (SymbolTexture != null)
        {
            var symbolImg = SymbolTexture.GetImage();
            int sw = (int)((SymbolRegion.Z - SymbolRegion.X) * TextureWidth * 0.5f);
            int sh = (int)((SymbolRegion.W - SymbolRegion.Y) * TextureHeight);
            symbolImg.Resize(sw, sh, Image.Interpolation.Nearest);
            int sx = (int)(SymbolRegion.X * TextureWidth * 0.5f);
            int sy = (int)(SymbolRegion.Y * TextureHeight);
            BlitLayer(symbolImg,
                new Rect2I(Vector2I.Zero, symbolImg.GetSize()),
                new Vector2I(sx, sy), true, img);
        }
        
        if (AttrBoxTexture != null)
        {
            var attrImg = AttrBoxTexture.GetImage();
            int cw = (int)((AttrBoxRegion.Z - AttrBoxRegion.X) * TextureWidth * 0.5f);
            int ch = (int)((AttrBoxRegion.W - AttrBoxRegion.Y) * TextureHeight);
            attrImg.Resize(cw, ch, Image.Interpolation.Nearest);
            int cx = (int)(AttrBoxRegion.X * TextureWidth * 0.5f);
            int cy = (int)(AttrBoxRegion.Y * TextureHeight);
            BlitLayer(attrImg,
                new Rect2I(Vector2I.Zero, attrImg.GetSize()),
                new Vector2I(cx, cy), true, img);
        }
        

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