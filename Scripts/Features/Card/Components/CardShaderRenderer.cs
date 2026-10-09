using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;
using CardCleaner.Scripts.Features.Card.Models.Effects;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardShaderRenderer : Node, ICardComponent
{
    private bool _baked;

    private Node _cardRoot = null!;

    private Vector3[] _gemEmissionColors = new Vector3[8];
    private float[] _gemEmissionStrengths = new float[8];

    private ICardMaterialComponent? _materialManager;

    // --- Text fields (front only) ---
    [Export] public Label3D NameLabel { get; set; } = null!;
    [Export] public Label3D AttrLabel { get; set; } = null!;


    public void Setup(Node cardRoot)
    {
        _cardRoot = cardRoot;
        _materialManager = _cardRoot.GetNode<CardMaterialManager>("MaterialManager");
    }


    public void Bake(CardTemplate template)
    {
        if (_baked) return;
        CallDeferred(MethodName.DeferredBake, template);
        _baked = true;
    }

    private void DeferredBake(CardTemplate template)
    {
        var box = GetParent().GetNodeOrNull<MeshInstance3D>("OuterBox_Baked");
        if (box == null || _materialManager == null)
        {
            CallDeferred(MethodName.DeferredBake, template);
            return;
        }

        var layers = template.GatherAllLayers();
        _materialManager.SetLayerTextures(layers);
        var material = _materialManager.ApplyMaterial(box);

        if (material == null) return;
        var blacklightController = _cardRoot.GetNodeOrNull<BlacklightController>("BlacklightController");
        blacklightController?.UpdateBlacklightEffect(material);
    }

    public virtual void SetGemEmission(int index, Color color, float strength)
    {
        if (_materialManager == null)
        {
            CallDeferred(MethodName.SetGemEmission, index, color, strength);
            return;
        }

        _materialManager.SetGemEmission(index, color, strength);
    }

    public virtual int SetArtEffects(RarityEffect rarity, ConditionEffect condition, float seed, Texture2D? normalMap)
    {
        if (_materialManager == null)
        {
            ILog.Error("Card renderer has no material manager; the art effects were not applied.");
            // 0 names no request, so a map delivered for it is dropped.
            return 0;
        }

        return _materialManager.SetArtEffects(rarity, condition, seed, normalMap);
    }

    public virtual void SetArtNormalMap(int request, RarityEffect rarity, Texture2D normalMap)
    {
        _materialManager?.SetArtNormalMap(request, rarity, normalMap);
    }

    public bool ArtEffectsEnabled
    {
        // Without a material manager no effects can show, so the flag reads false.
        get => _materialManager?.ArtEffectsEnabled ?? false;
        set
        {
            if (_materialManager == null)
            {
                ILog.Error("Card renderer has no material manager; the art effects were not switched.");
                return;
            }

            _materialManager.ArtEffectsEnabled = value;
        }
    }
}
