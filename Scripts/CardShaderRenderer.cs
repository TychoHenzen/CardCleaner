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

    // --- Blacklight security effect controls ---
    [Export] public Color FluorescentColor { get; set; } = new Color(0.0f, 1.0f, 0.5f);
    [Export] public float FluorescentIntensity { get; set; } = 5.0f;
    [Export] public float VisibilityThreshold { get; set; } = 0.1f;
    [Export] public float BlacklightRange { get; set; } = 5.0f;

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
    private RigidBody3D _cardRoot;
    private ShaderMaterial _activeMaterial;

    public void Setup(RigidBody3D cardRoot)
    {
        _cardRoot = cardRoot;
    }

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
        {
            SetPhysicsProcess(true);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || _cardRoot == null || _activeMaterial == null) return;

        // Calculate blacklight exposure
        float exposure = CalculateBlacklightExposure();
        _activeMaterial.SetShaderParameter("blacklight_exposure", exposure);
    }

    private float CalculateBlacklightExposure()
    {
        // Find the player's blacklight
        var player = GetTree().GetFirstNodeInGroup("player");
        if (player == null) return 0.0f;

        var spotlight = player.GetNodeOrNull<SpotLight3D>("Head/Camera3D/SpotLight3D");
        if (spotlight == null || !spotlight.Visible) return 0.0f;

        var cardPos = _cardRoot.GlobalPosition;
        var lightPos = spotlight.GlobalPosition;
        var lightForward = -spotlight.GlobalTransform.Basis.Z;

        // Calculate distance factor
        float distance = lightPos.DistanceTo(cardPos);
        if (distance > BlacklightRange) return 0.0f;

        float distanceFactor = 1.0f - (distance / BlacklightRange);

        // Calculate angle factor
        var toCard = (cardPos - lightPos).Normalized();
        float angle = lightForward.Dot(toCard);
        float spotAngleRad = Mathf.DegToRad(spotlight.SpotAngle);
        
        if (angle < Mathf.Cos(spotAngleRad)) return 0.0f;

        float angleFactor = (angle - Mathf.Cos(spotAngleRad)) / (1.0f - Mathf.Cos(spotAngleRad));

        // Combine factors
        return Mathf.Clamp(distanceFactor * angleFactor * spotlight.LightEnergy, 0.0f, 1.0f);
    }

    public void Bake()
    {
        if (_baked) return;

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

        // Build arrays for shader uniforms
        var texturesArr   = new Godot.Collections.Array<Texture2D>();
        var regionsArr    = new Godot.Collections.Array<Vector4>();
        var frontFlagsArr = new Godot.Collections.Array<bool>();
        var backFlagsArr  = new Godot.Collections.Array<bool>();
        var gemFlagsArr   = new Godot.Collections.Array<bool>();

        // Mark gem layers (last 8 layers are gems)
        int gemStartIndex = allLayers.Length - Gems.Length;

        for (int i = 0; i < allLayers.Length; i++) {
            var ld = allLayers[i];
            texturesArr.Add(ld.Texture);
            regionsArr.Add(ld.Region);
            frontFlagsArr.Add(ld.RenderOnFront);
            backFlagsArr.Add(ld.RenderOnBack);
            
            bool isGem = i >= gemStartIndex;
            gemFlagsArr.Add(isGem);
        }

        // Create material
        if (CardMaterial?.Duplicate() is not ShaderMaterial mat)
            return;

        // Set all shader parameters
        mat.SetShaderParameter("textures", texturesArr);
        mat.SetShaderParameter("regions", regionsArr);
        mat.SetShaderParameter("frontFlags", frontFlagsArr);
        mat.SetShaderParameter("backFlags", backFlagsArr);
        mat.SetShaderParameter("gem_flags", gemFlagsArr);
        
        // Security effect parameters
        mat.SetShaderParameter("blacklight_exposure", 0.0f);
        mat.SetShaderParameter("fluorescent_color", new Vector3(FluorescentColor.R, FluorescentColor.G, FluorescentColor.B));
        mat.SetShaderParameter("fluorescent_intensity", FluorescentIntensity);
        mat.SetShaderParameter("visibility_threshold", VisibilityThreshold);

        box.MaterialOverride = mat;
        _activeMaterial = mat;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // No physics integration needed
    }
}