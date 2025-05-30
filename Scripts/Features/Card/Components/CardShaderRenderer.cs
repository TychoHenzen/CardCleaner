using System.Linq;
using CardCleaner.Scripts.Controllers;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

[Tool]
public partial class CardShaderRenderer : Node
{

    // --- Blacklight security effect controls ---
    [Export] public float BlacklightRange { get; set; } = 5.0f;

    // --- Text fields (front only) ---
    [Export] public Font TextFont { get; set; }
    [Export] public Color TextColor { get; set; } = new Color(0, 0, 0, 1);
    [Export] public string NameText { get; set; } = "";
    [Export] public Label3D NameLabel { get; set; }
    [Export] public string AttributesText { get; set; } = "";
    [Export] public Label3D AttrLabel { get; set; }
    
    private Vector3[] _gemEmissionColors    = new Vector3[8];
    private float[]   _gemEmissionStrengths = new float[8];
    
    private ICardMaterialComponent _materialManager;
    private IBlacklightController _blacklightController;
    private bool _baked = false;

    private RigidBody3D _cardRoot;
    

    public void Setup(RigidBody3D cardRoot)
    {
        _cardRoot = cardRoot;
        _materialManager = GetNode<CardMaterialManager>("MaterialManager");
        _blacklightController = GetNode<BlacklightController>("BlacklightController");
    }
    
    
    public void Bake(CardTemplate template)
    {
        if (_baked) return;
        CallDeferred(nameof(DeferredBake),template);
        _baked = true;
    }

    private void DeferredBake(CardTemplate template)
    {
        var box = GetParent().GetNodeOrNull<MeshInstance3D>("OuterBox_Baked");
        if (box == null)
        {
            CallDeferred(nameof(DeferredBake));
            return;
        }
        var layers = template.GatherAllLayers();
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
    public void SetGemEmission(int index, Color color, float strength)
    {
        _materialManager.SetGemEmission(index, color, strength);
    }
}