using System.Linq;
using CardCleaner.Scripts.Controllers;
using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

[Tool]
public partial class CardShaderRenderer : Node, IRenderComponent
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
    {
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.25f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.4f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.55f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.7f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.25f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.4f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.55f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.7f, 0.1f, 0.075f) },
    };

    public LayerData[] Gems { get; set; } =
    {
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.25f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.4f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.55f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.9f, 0.7f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.25f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.4f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.55f, 0.1f, 0.075f) },
        new() { RenderOnFront = true, Region = new Vector4(0.0f, 0.7f, 0.1f, 0.075f) },
    };

    // --- Blacklight security effect controls ---
    [Export] public float BlacklightRange { get; set; } = 5.0f;

    // --- Text fields (front only) ---
    [Export] public Font TextFont { get; set; }
    [Export] public Color TextColor { get; set; } = new Color(0, 0, 0, 1);
    [Export] public string NameText { get; set; } = "";
    [Export] public Label3D NameLabel { get; set; }
    [Export] public string AttributesText { get; set; } = "";
    [Export] public Label3D AttrLabel { get; set; }

    // --- Material to receive baked texture ---
    // [Export] public ShaderMaterial CardMaterial { get; set; }
    
    private Vector3[] _gemEmissionColors    = new Vector3[8];
    private float[]   _gemEmissionStrengths = new float[8];
    
    private ICardMaterialComponent _materialManager;
    private IBlacklightController _blacklightController;
    private bool _baked = false;

    private RigidBody3D _cardRoot;
    // private ShaderMaterial _activeMaterial;

    public void Setup(RigidBody3D cardRoot)
    {
        _cardRoot = cardRoot;
        _materialManager = GetNode<CardMaterialManager>("MaterialManager");
        _blacklightController = GetNode<BlacklightController>("BlacklightController");
    }
    
    
    public void Bake()
    {
        if (_baked) return;
        CallDeferred(nameof(DeferredBake));
        _baked = true;
    }

    private void DeferredBake()
    {
        var box = GetParent().GetNodeOrNull<MeshInstance3D>("OuterBox_Baked");
        if (box == null)
        {
            CallDeferred(nameof(DeferredBake));
            return;
        }
        var layers = GatherAllLayers();
        _materialManager.SetLayerTextures(layers);
        _materialManager.ApplyMaterial(box);
        
        if (box.MaterialOverride is ShaderMaterial material)
        {
            CallDeferred(nameof(EnableBlacklightUpdates), material);
        }
    }
    
    private void EnableBlacklightUpdates(ShaderMaterial material)
    {
        _blacklightController?.UpdateBlacklightEffect(material);
    }
    private LayerData[] GatherAllLayers()
    {
        return new[] { CardBase, Border, Corners, ImageBackground, DescriptionBox, Art, Banner, Symbol, EnergyFill1, EnergyFill2, EnergyContainer }
            .Concat(GemSockets)
            .Concat(Gems)
            .Reverse()
            .ToArray();
    }
    /// <summary>
    /// Called by generator to supply color and strength for gem index.
    /// </summary>
    /// 
    public void SetGemEmission(int index, Color color, float strength)
    {
        _materialManager.SetGemEmission(index, color, strength);
    }
}