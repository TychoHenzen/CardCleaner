using Godot;
using CardCleaner.Scripts.Interfaces;

namespace CardCleaner.Scripts
{
    [Tool]
    [GlobalClass]
    public partial class LayerData : Resource
    {
        [Export] public Texture2D Texture { get; set; }
        [Export] public Vector4 Region { get; set; } = new(0, 0, 1, 1);
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
        public LayerData CardBase { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };
        public LayerData Border { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };

        public LayerData Corners { get; set; } = new() { RenderOnFront = true, RenderOnBack = true };

        // --- LayerData for paired texture+region+side ---
        public LayerData Art { get; set; } =
            new() { RenderOnFront = true, Region = new Vector4(0.1f, 0.1f, 0.8f, 0.35f) };

        public LayerData Symbol { get; set; } = new()
            { RenderOnBack = true, RenderOnFront = false, Region = new Vector4(0.2f, 0.2f, 0.6f, 0.6f) };

        public LayerData ImageBackground { get; set; } =
            new() { RenderOnFront = true, Region = new Vector4(0, 0, 1f, 0.5f) };

        public LayerData Banner { get; set; } = new()
            { RenderOnFront = true, Region = new Vector4(0.1f, 0.43f, 0.8f, 0.2f) };

        public LayerData DescriptionBox { get; set; } = new()
            { RenderOnFront = true, Region = new Vector4(0.015f, 0.43f, 1.01f, 0.59f) };

        public LayerData EnergyContainer { get; set; } =
            new() { RenderOnFront = true, Region = new Vector4(0.8f, 0, 0.2f, 0.15f) };

        public LayerData EnergyFill1 { get; set; } =
            new() { RenderOnFront = true, Region = new Vector4(0.8f, 0, 0.2f, 0.15f) };

        public LayerData EnergyFill2 { get; set; } =
            new() { RenderOnFront = true, Region = new Vector4(0.8f, 0, 0.2f, 0.15f) };


        public LayerData[] GemSockets { get; set; } =
        [
            new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.25f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.4f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.55f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.7f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.25f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.4f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.55f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.7f, 0.1f, 0.075f) },
        ];

        public LayerData[] Gems { get; set; } =
        [
            new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.25f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.4f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.55f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.7f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.25f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.4f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.55f, 0.1f, 0.075f) },
            new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.7f, 0.1f, 0.075f) },
        ];

        // --- Text fields (front only) ---
        [Export] public Font TextFont { get; set; }
        [Export] public Color  TextColor  { get; set; } = new Color(0, 0, 0, 1);
        [Export] public string NameText { get; set; } = "";
        [Export] public Label3D NameLabel { get; set; }
        [Export] public string AttributesText { get; set; } = "";
        [Export] public Label3D AttrLabel { get; set; }

        // --- Material to receive baked texture ---
        [Export] public StandardMaterial3D CardMaterial { get; set; }

        private bool _baked = false;

        public void Setup(RigidBody3D cardRoot)
        {
            if (_baked)
                return;

            CardMaterial ??= new StandardMaterial3D();

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

        private void DeferredAssign()
        {
            var box = GetParent().GetNodeOrNull<MeshInstance3D>("OuterBox_Baked");
            if (box == null)
            {
                CallDeferred(nameof(DeferredAssign));
                return;
            }

            // Bake material as before…
            var mat = CardMaterial?.Duplicate() as StandardMaterial3D;
            if (mat == null) return;
            mat.AlbedoTexture = GenerateCompositeTexture();
            mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            mat.AlphaScissorThreshold = 0.5f;
            box.MaterialOverride = mat;
        }

    }
}