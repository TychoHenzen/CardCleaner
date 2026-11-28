using System;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>
///     One item laid on the checkout counter for scanning. Interacting scans it once through <see cref="OnScan" />.
///     It shows the item itself when the customer kept a copy of it (<see cref="Look" />), else a grey box. The aim
///     draws the shared edge highlight around it. A scanned item goes into the bag: it lifts and shrinks away in a
///     moment and stops colliding at once, so the counter holds only what is still to scan, and the register display
///     shows the running subtotal. Items stacked on it settle down into its place (<see cref="SettleBy" />).
/// </summary>
public partial class CheckoutItemProp : StaticBody3D, IInteractable
{
    /// <summary>How far the base of the item sits below the item spot marker: the spot marks the middle of a box.</summary>
    public const float BaseDrop = 0.03f;

    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask
    private const float MinimumSide = 0.04f;
    private const float HighlightMargin = 0.04f;
    private const float BagSeconds = 0.25f;
    private const float BagLift = 0.12f;
    private const float SettleSeconds = 0.15f;
    private const string HighlightMaterialPath = "res://Data/Ui/AimHighlightMaterial.tres";
    private static readonly Vector3 PropSize = new(0.2f, 0.06f, 0.28f);
    private static readonly Color IdleColor = new(0.6f, 0.6f, 0.6f);

    private readonly Node3D _visual = new() { Name = "Visual" };
    private MeshInstance3D? _highlight;
    private float? _restHeight;

    /// <summary>The id the counter gave this item; passed to <see cref="OnScan" />.</summary>
    public int ItemId { get; set; }

    /// <summary>The item spot this prop is stacked on.</summary>
    public int Spot { get; set; }

    /// <summary>
    ///     The still copy of the real item, adopted as a child when the prop is ready. Null shows a grey box.
    /// </summary>
    public StockItemLook? Look { get; set; }

    /// <summary>Set by the counter; called with <see cref="ItemId" /> when an unscanned item is used.</summary>
    public Action<int>? OnScan { get; set; }

    public bool Scanned { get; private set; }

    /// <summary>The size of what the prop shows, so the counter can stack items without overlap.</summary>
    public Vector3 Size => Look?.Size.Max(new Vector3(MinimumSide, MinimumSide, MinimumSide)) ?? PropSize;

    /// <summary>The height above the counter this prop rests at once it has settled.</summary>
    public float RestHeight => _restHeight ?? Position.Y;

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

    /// <summary>Marks the item scanned and puts it into the bag.</summary>
    public void MarkScanned()
    {
        Scanned = true;
        ClearHighlight();
        CollisionLayer = 0;
        if (!IsInsideTree())
        {
            Visible = false;
            return;
        }

        // The visual shrinks, not the body: a scaled physics body is not supported.
        var tween = CreateTween();
        tween.TweenProperty(_visual, "scale", Vector3.One * 0.01f, BagSeconds)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(_visual, "position:y", BagLift, BagSeconds);
        tween.TweenCallback(Callable.From(() => Visible = false));
    }

    /// <summary>Lowers the prop by <paramref name="drop" /> metres, as when the item under it is bagged.</summary>
    public void SettleBy(float drop)
    {
        _restHeight = RestHeight - drop;
        if (!IsInsideTree())
        {
            Position = Position with { Y = _restHeight.Value };
            return;
        }

        CreateTween().TweenProperty(this, "position:y", _restHeight.Value, SettleSeconds)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
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
        AddChild(_visual);

        if (Look != null)
        {
            Look.Node.Position = new Vector3(0f, -BaseDrop, 0f);
            _visual.AddChild(Look.Node);
        }
        else
        {
            var material = new StandardMaterial3D { AlbedoColor = IdleColor };
            _visual.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = PropSize, Material = material } });
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
    }
}
