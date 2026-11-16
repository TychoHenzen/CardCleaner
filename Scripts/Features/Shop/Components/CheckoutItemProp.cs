using System;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     One item laid on the checkout counter for scanning. Interacting scans it once through <see cref="OnScan" />.
///     It shows the item itself when the customer kept a copy of it (<see cref="Look" />), else a grey box. The aim
///     draws the shared edge highlight around it, and a scanned item shows its price in green above it.
/// </summary>
public partial class CheckoutItemProp : StaticBody3D, IInteractable
{
    /// <summary>How far the base of the item sits below the item spot marker: the spot marks the middle of a box.</summary>
    public const float BaseDrop = 0.03f;

    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask
    private const float MinimumSide = 0.04f;
    private const float HighlightMargin = 0.04f;
    private const float PriceLift = 0.08f;
    private const string HighlightMaterialPath = "res://Data/Ui/AimHighlightMaterial.tres";
    private static readonly Vector3 PropSize = new(0.2f, 0.06f, 0.28f);
    private static readonly Color IdleColor = new(0.6f, 0.6f, 0.6f);
    private static readonly Color ScannedColor = new(0.3f, 0.75f, 0.35f);
    private static readonly Color PriceColor = new(0.45f, 1f, 0.5f);

    private StandardMaterial3D? _material;
    private MeshInstance3D? _highlight;
    private Label3D? _priceLabel;

    /// <summary>The id the counter gave this item; passed to <see cref="OnScan" />.</summary>
    public int ItemId { get; set; }

    /// <summary>The asking price, shown once the item is scanned.</summary>
    public int Price { get; set; }

    /// <summary>The still copy of the real item, adopted as a child when the prop is ready. Null shows a grey box.</summary>
    public StockItemLook? Look { get; set; }

    /// <summary>Set by the counter; called with <see cref="ItemId" /> when an unscanned item is used.</summary>
    public Action<int>? OnScan { get; set; }

    public bool Scanned { get; private set; }

    /// <summary>The size of what the prop shows, so the counter can stack items without overlap.</summary>
    public Vector3 Size => Look?.Size.Max(new Vector3(MinimumSide, MinimumSide, MinimumSide)) ?? PropSize;

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    public bool CanInteract => !Scanned;

    public Node3D InteractionBody => this;

    public string Prompt => "Scan item";

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
        ClearHighlight();
        if (_material != null)
            _material.AlbedoColor = ScannedColor;
        if (_priceLabel != null)
        {
            _priceLabel.Text = Price.ToString();
            _priceLabel.Visible = true;
        }
    }

    public void Highlight()
    {
        if (!Scanned && _highlight != null)
            _highlight.Visible = true;
    }

    public void ClearHighlight()
    {
        if (_highlight != null)
            _highlight.Visible = false;
    }

    private void BuildVisual()
    {
        var size = Size;
        var middle = new Vector3(0f, size.Y / 2f - BaseDrop, 0f);
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = middle });

        if (Look != null)
        {
            Look.Node.Position = new Vector3(0f, -BaseDrop, 0f);
            AddChild(Look.Node);
        }
        else
        {
            _material = new StandardMaterial3D { AlbedoColor = IdleColor };
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = PropSize, Material = _material } });
        }

        _highlight = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = size + Vector3.One * HighlightMargin,
                Material = GD.Load<Material>(HighlightMaterialPath)
            },
            Position = middle,
            Visible = false
        };
        AddChild(_highlight);

        _priceLabel = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = PriceColor,
            OutlineSize = 8,
            PixelSize = 0.002f,
            FontSize = 48,
            Position = new Vector3(0f, size.Y - BaseDrop + PriceLift, 0f),
            Visible = false
        };
        AddChild(_priceLabel);
    }
}
