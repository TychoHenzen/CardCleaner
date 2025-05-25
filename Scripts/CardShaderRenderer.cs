using System.Linq;
using CardCleaner.Scripts;
using Godot;
using CardCleaner.Scripts.Interfaces;

[Tool]
public partial class CardShaderRenderer : Node, ICardComponent
{
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
    [Export] public Color TextColor { get; set; } = new Color(0, 0, 0, 1);
    [Export] public string NameText { get; set; } = "";
    [Export] public Label3D NameLabel { get; set; }
    [Export] public string AttributesText { get; set; } = "";
    [Export] public Label3D AttrLabel { get; set; }

    // --- Material to receive baked texture ---
    [Export] public ShaderMaterial CardMaterial { get; set; }

    private bool _baked = false;

    public void Setup(RigidBody3D cardRoot)
    {
    }

    public void Bake()
    {
        if (_baked)
            return;

        CallDeferred(nameof(DeferredAssign));
        _baked = true;
    }

    private void DeferredAssign()
    {
        var box = GetParent().GetNodeOrNull<MeshInstance3D>("OuterBox_Baked");
        if (box == null)
        {
            CallDeferred(nameof(DeferredAssign));
            return;
        }

        // Gather all LayerData into arrays
        var allLayers = new[]
            {
                CardBase, Border, Corners, ImageBackground, DescriptionBox,
                Art, Banner, Symbol, EnergyContainer, EnergyFill1, EnergyFill2
            }
            .Concat(GemSockets)
            .Concat(Gems)
            .Reverse()
            .ToArray();

        // 2) Build Godot arrays for each uniform
        var texturesArr   = new Godot.Collections.Array<Texture2D>();
        var regionsArr    = new Godot.Collections.Array<Vector4>();
        var frontFlagsArr = new Godot.Collections.Array<bool>();
        var backFlagsArr  = new Godot.Collections.Array<bool>();

        foreach (var ld in allLayers) {
            texturesArr.Add(ld.Texture);
            regionsArr.Add(ld.Region);
            frontFlagsArr.Add(ld.RenderOnFront);
            backFlagsArr.Add(ld.RenderOnBack);
        }

        // 3) Duplicate the base material and set uniforms in bulk
        if (CardMaterial?.Duplicate() is not ShaderMaterial mat)
            return;

        
        mat.SetShaderParameter("textures",   texturesArr);
        mat.SetShaderParameter("regions",    regionsArr);
        mat.SetShaderParameter("frontFlags", frontFlagsArr);
        mat.SetShaderParameter("backFlags",  backFlagsArr);

        box.MaterialOverride = mat;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // no-op
    }
}