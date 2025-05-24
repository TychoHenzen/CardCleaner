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

        // Generate and hold the baked front+back atlas
        GenerateFrontTexture();
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

    private static Vector4 Remap(Vector4 r) => new(r.X * 0.5f, r.Y, r.Z * 0.5f, r.W);

    private SubViewport _subViewport;
    private void GenerateFrontTexture()
    {
        // 1) Instantiate offscreen SubViewport
        if (ViewportScene.Instantiate() is not SubViewport vp)
        {
            GD.PrintErr("ViewportScene must be a SubViewport.tscn");
            return;
        }

        _subViewport = vp;
        AddChild(_subViewport);

        _subViewport.Size = new Vector2I(TextureWidth, TextureHeight);
        _subViewport.TransparentBg = true;
        _subViewport.SetUpdateMode(SubViewport.UpdateMode.Once);

        // 2) Root Control
        var root = new Control { Size = _subViewport.Size };
        _subViewport.AddChild(root);

        // 3) Draw back into right half (0.5→1)
        root.AddChild(PlaceLayer(BackTexture, new Vector4(0.5f, 0, 1f, 1f)));

        // 4) Draw front layers into left half (remap X/Z by *0.5)
        root.AddChild(PlaceLayer(FrontTemplate, new Vector4(0, 0, 0.5f, 1f)));
        if (BorderTexture != null)
            root.AddChild(PlaceLayer(BorderTexture, Remap(BorderRegion)));
        if (ArtTexture != null)
            root.AddChild(PlaceLayer(ArtTexture, Remap(ArtRegion)));
        if (SymbolTexture != null)
            root.AddChild(PlaceLayer(SymbolTexture, Remap(SymbolRegion)));
        if (AttrBoxTexture != null)
            root.AddChild(PlaceLayer(AttrBoxTexture, Remap(AttrBoxRegion)));

        // 5) Text on front
        root.AddChild(MakeLabel(NameText, Remap(NameRegion)));
        root.AddChild(MakeLabel(AttributesText, Remap(AttrTextRegion)));
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
        Texture2D result = _subViewport.GetTexture();
        _subViewport.QueueFree();
        // Apply our material (with AlbedoTexture == _pendingTexture)
        CardMaterial.AlbedoTexture = result;
        box.MaterialOverride = CardMaterial;
    }
}