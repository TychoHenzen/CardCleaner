using Godot;
using CardCleaner.Scripts.Interfaces;

namespace CardCleaner.Scripts
{
    [Tool]
    public partial class LayerData : Resource
    {
        [Export] public Texture2D Texture { get; set; }
        [Export] public Vector4 Region { get; set; } = new(0,0,1,1);
        [Export] public bool RenderOnFront { get; set; } = true;
        [Export] public bool RenderOnBack { get; set; } = false;
    }

    [Tool]
    public partial class CardRenderer : Node, ICardComponent
    {
        // --- Atlas resolution ---
        [Export] public int TextureWidth { get; set; } = 1024;
        [Export] public int TextureHeight { get; set; } = 1024;

        // --- Editor bake toggle ---
        [Export] public bool BakeInEditor { get; set; } = false;

        // --- Front & back base templates ---
        [Export] public LayerData CardBase { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };
        [Export] public LayerData Border { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };

        [Export] public LayerData Corners { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };

        // --- LayerData for paired texture+region+side ---
        [Export] public LayerData Art { get; set; } = new() { RenderOnFront = true , Region = new Vector4(0.1f, 0.1f, 0.8f, 0.35f)};
        [Export] public LayerData Symbol { get; set; } = new() { RenderOnBack = true, RenderOnFront = false, Region = new Vector4(0.2f, 0.2f, 0.6f, 0.6f) };

        [Export] public LayerData ImageBackground { get; set; } = new() { RenderOnFront = true, Region = new Vector4(0, 0, 1f, 0.5f) };
        [Export] public LayerData Banner { get; set; } = new() { RenderOnFront = true, Region = new Vector4(0.1f, 0.43f, 0.8f, 0.2f) };
        [Export] public LayerData DescriptionBox { get; set; } = new() { RenderOnFront = true, Region = new Vector4(0.015f, 0.43f, 1.01f, 0.59f) };
        [Export] public LayerData EnergyContainer { get; set; } = new() { RenderOnFront = true , Region = new Vector4(0.8f, 0, 0.2f, 0.15f)};
        [Export] public LayerData EnergyFill1 { get; set; } = new() { RenderOnFront = true, Region = new Vector4(0.8f, 0, 0.2f, 0.15f)};
        [Export] public LayerData EnergyFill2 { get; set; } = new() { RenderOnFront = true , Region = new Vector4(0.8f, 0, 0.2f, 0.15f)};

        [Export] public LayerData[] GemSockets { get; set; } = new LayerData[8];
        [Export] public LayerData[] Gems { get; set; } = new LayerData[8];

        // --- Text fields (front only) ---
        [Export] public Font TextFont { get; set; }
        [Export] public string NameText { get; set; } = "";
        [Export] public Vector4 NameRegion { get; set; }
        [Export] public string AttributesText { get; set; } = "";
        [Export] public Vector4 AttrTextRegion { get; set; }

        // --- Material to receive baked texture ---
        [Export] public StandardMaterial3D CardMaterial { get; set; }

        private bool _baked = false;

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() && BakeInEditor)
            {
                BakeInEditor = false;
                CallDeferred(nameof(DeferredEditorBake));
            }
        }

        public void Setup(RigidBody3D cardRoot)
        {
            if (_baked || CardMaterial == null)
                return;

            CallDeferred(nameof(DeferredAssign));
            _baked = true;
        }

        public void IntegrateForces(PhysicsDirectBodyState3D state)
        {
        }

        private void BlitLayer(Image srcImg, Rect2I srcRect, Vector2I dstPos, bool alphaBlend, Image dstImg)
        {
            if (srcImg == null) return;
            if (srcImg.GetFormat() != dstImg.GetFormat())
                srcImg.Convert(dstImg.GetFormat());
            if (alphaBlend)
                dstImg.BlendRect(srcImg, srcRect, dstPos);
            else
                dstImg.BlitRect(srcImg, srcRect, dstPos);
        }

        private void Blend(LayerData layer, Image img)
        {
            if (layer == null || layer.Texture == null) return;
            var region = layer.Region;
            var src = layer.Texture.GetImage();
            int w = (int)(region.Z * TextureWidth * 0.5f);
            int h = (int)(region.W * TextureHeight);
            src.Resize(w, h, Image.Interpolation.Nearest);
            if (layer.RenderOnFront)
            {
                
                BlitImage(img, region, src, 0);
            }
            if (layer.RenderOnBack)
            {
                BlitImage(img, region, src, TextureWidth / 2);
            }
        }

        private void BlitImage(Image img, Vector4 region, Image src, int xOffset)
        {
            int x = xOffset + (int)(region.X * TextureWidth * 0.5f);
            int y = (int)(region.Y * TextureHeight);
            BlitLayer(src, new Rect2I(Vector2I.Zero, src.GetSize()), new Vector2I(x, y), true, img);
        }

        private ImageTexture GenerateCompositeTexture()
        {
            var img = Image.CreateEmpty(TextureWidth, TextureHeight, false, Image.Format.Rgba8);
            img.Fill(new Color(1f, 1f, 1f, 0f));

            // Back half
            Blend(CardBase, img);
            Blend(Border, img);
            Blend(Corners, img);

            // Shared front/back layers
            Blend(ImageBackground, img);
            Blend(DescriptionBox, img);
            Blend(Art, img);
            Blend(Banner, img);
            Blend(Symbol, img);

            // Front-only layers
            Blend(EnergyFill1, img);
            Blend(EnergyFill2, img);
            Blend(EnergyContainer, img);

            // Gem sockets and gems
            foreach (var socket in GemSockets) Blend(socket, img);
            foreach (var gem in Gems) Blend(gem, img);

            var atlas = ImageTexture.CreateFromImage(img);
            atlas.SetImage(img);
            return atlas;
        }

        private void DeferredEditorBake()
        {
            var box = GetParent().GetNodeOrNull<CsgBox3D>("OuterBox");
            if (box == null)
            {
                GD.PrintErr("OuterBox not found for editor bake.");
                return;
            }

            if (CardMaterial.Duplicate() is not StandardMaterial3D mat) return;
            mat.AlbedoTexture = GenerateCompositeTexture();
            mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            mat.AlphaScissorThreshold = 0.5f;
            box.MaterialOverride = mat;
            box.AddChild(MakeLabel(NameText, NameRegion));
            box.AddChild(MakeLabel(AttributesText, AttrTextRegion));
        }

        private void DeferredAssign()
        {
            var box = GetParent().GetNodeOrNull<MeshInstance3D>("OuterBox_Baked");
            if (box == null)
                CallDeferred(nameof(DeferredAssign));
            var mat = CardMaterial.Duplicate() as StandardMaterial3D;
            if (mat == null) return;
            mat.AlbedoTexture = GenerateCompositeTexture();
            mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            mat.AlphaScissorThreshold = 0.5f;
            box.MaterialOverride = mat;
            box.AddChild(MakeLabel(NameText, NameRegion));
            box.AddChild(MakeLabel(AttributesText, AttrTextRegion));
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
    }
}