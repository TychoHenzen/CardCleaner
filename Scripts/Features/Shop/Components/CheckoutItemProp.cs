using System;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     One item laid on the checkout counter for scanning. Interacting scans it once through <see cref="OnScan" />.
///     Stand-in art: a grey box, green once scanned.
/// </summary>
public partial class CheckoutItemProp : StaticBody3D, IInteractable
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask
    private static readonly Vector3 PropSize = new(0.2f, 0.06f, 0.28f);
    private static readonly Color IdleColor = new(0.6f, 0.6f, 0.6f);
    private static readonly Color HighlightColor = new(0.8f, 0.8f, 0.8f);
    private static readonly Color ScannedColor = new(0.3f, 0.75f, 0.35f);

    private StandardMaterial3D? _material;

    /// <summary>The id the counter gave this item; passed to <see cref="OnScan" />.</summary>
    public int ItemId { get; set; }

    /// <summary>Set by the counter; called with <see cref="ItemId" /> when an unscanned item is used.</summary>
    public Action<int>? OnScan { get; set; }

    public bool Scanned { get; private set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    public bool CanInteract => !Scanned;

    public Node3D InteractionBody => this;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        CollisionMask = 0;
        BuildVisual();
    }

    public void Interact()
    {
        if (!Scanned)
            OnScan?.Invoke(ItemId);
    }

    public void MarkScanned()
    {
        Scanned = true;
        Paint(ScannedColor);
    }

    public void Highlight()
    {
        if (!Scanned)
            Paint(HighlightColor);
    }

    public void ClearHighlight()
    {
        if (!Scanned)
            Paint(IdleColor);
    }

    private void BuildVisual()
    {
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = PropSize } });
        _material = new StandardMaterial3D { AlbedoColor = IdleColor };
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = PropSize, Material = _material } });
    }

    private void Paint(Color color)
    {
        if (_material != null)
            _material.AlbedoColor = color;
    }
}
