using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

[Tool]
public partial class CardShaderRenderer : Node, ICardComponent
{
    private bool _baked;

    private RigidBody3D _cardRoot;

    private Vector3[] _gemEmissionColors = new Vector3[8];
    private float[] _gemEmissionStrengths = new float[8];

    private ICardMaterialComponent _materialManager;

    // --- Text fields (front only) ---
    [Export] public Label3D NameLabel { get; set; }
    [Export] public Label3D AttrLabel { get; set; }


    public void Setup(RigidBody3D cardRoot)
    {
        _cardRoot = cardRoot;
        _materialManager = _cardRoot.GetNodeOrNull<CardMaterialManager>("MaterialManager");
    }


    public void Bake(Core.Data.CardTemplate template)
    {
        if (_baked) return;
        CallDeferred(nameof(DeferredBake), template);
        _baked = true;
    }

    private void DeferredBake(Core.Data.CardTemplate template)
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
            CallDeferred(nameof(EnableBlacklightUpdates), material);
    }

    private void EnableBlacklightUpdates(ShaderMaterial material)
    {
        var blacklightController = _cardRoot.GetNodeOrNull<BlacklightController>("BlacklightController");
        blacklightController?.UpdateBlacklightEffect(material);
    }

    public void SetGemEmission(int index, Color color, float strength)
    {
        if (_materialManager == null)
        {
            CallDeferred(nameof(SetGemEmission), index, color, strength);
            return;
        }

        _materialManager.SetGemEmission(index, color, strength);
    }
}