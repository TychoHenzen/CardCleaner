using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

[Tool]
public partial class CardShaderRenderer : Node, ICardComponent
{
    private bool _baked;
    private Core.Data.CardTemplate _pendingTemplate; // ADD: Store template as instance variable

    private Node _cardRoot;

    private Vector3[] _gemEmissionColors = new Vector3[8];
    private float[] _gemEmissionStrengths = new float[8];

    private ICardMaterialComponent _materialManager;

    // --- Text fields (front only) ---
    [Export] public Label3D NameLabel { get; set; }
    [Export] public Label3D AttrLabel { get; set; }


    public void Setup(Node cardRoot)
    {
        _cardRoot = cardRoot;
        _materialManager = _cardRoot.GetNode<CardMaterialManager>("MaterialManager");
    }


    public void Bake(Core.Data.CardTemplate template)
    {
        if (_baked) return;
        _pendingTemplate = template; // CHG: Store template instead of passing through CallDeferred
        CallDeferred(nameof(DeferredBake)); // CHG: Remove template parameter
        _baked = true;
    }

    private void DeferredBake() // CHG: Remove parameter, use stored template
    {
        var box = GetParent().GetNodeOrNull<MeshInstance3D>("OuterBox_Baked");
        if (box == null)
        {
            // CHG: Use direct retry in next frame instead of recursive CallDeferred
            CallDeferred(nameof(DeferredBake)); 
            return;
        }

        var layers = _pendingTemplate.GatherAllLayers(); // CHG: Use stored template
        _materialManager.SetLayerTextures(layers);
        var material = _materialManager.ApplyMaterial(box);

        if (material == null) return;
        var blacklightController = _cardRoot.GetNodeOrNull<BlacklightController>("BlacklightController");
        blacklightController?.UpdateBlacklightEffect(material);
        
        _pendingTemplate = null; // ADD: Clear stored template when done
    }

    public virtual void SetGemEmission(int index, Color color, float strength)
    {
        if (_materialManager == null)
        {
            CallDeferred(nameof(SetGemEmission), index, color, strength);
            return;
        }

        _materialManager.SetGemEmission(index, color, strength);
    }
}