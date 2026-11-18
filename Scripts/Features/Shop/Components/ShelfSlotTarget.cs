using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The click target of one free shelf slot. Aimed at with a card, a shut container or an open box or pack in hand,
///     a click puts that stock (for an open one, the next item inside) in the slot. The body covers the slot's
///     trigger box and is on the interaction layer only while the shelf is shown and the slot is empty, or holds a
///     partial stack that takes what the hand offers now. So a full slot, or a partial booster stack aimed at with
///     nothing that fits it, leaves the stock itself to be clicked and picked up.
/// </summary>
public partial class ShelfSlotTarget : StaticBody3D, IInteractable, IDependent
{
    private const uint InteractableLayerBit = 4;
    private const float StockReach = 4.5f;
    private const float SurfaceMargin = 0.05f;
    private const string HighlightMaterialPath = "res://Data/Ui/AimHighlightMaterial.tres";

    private DeckSlot? _slot;
    private IServiceResolver? _services;
    private IStockHand? _hand;
    private MeshInstance3D? _highlight;

    /// <summary>The slot this target stocks.</summary>
    public DeckSlot? Slot => _slot;

    public bool CanInteract => _slot != null && _hand?.Offered is { } offer && _slot.CanTake(offer.Stock);

    public Node3D InteractionBody => this;

    public float InteractionRange => StockReach;

    public string Prompt => _hand?.Offered?.Prompt ?? "Put the card here";

    /// <summary>
    ///     Builds the target for <paramref name="slot" /> as a child of <paramref name="shelf" />. It covers the
    ///     slot's trigger box and reaches down to the slot's own surface, where the stock lies, so aiming at the spot
    ///     the stock goes hits it. Null when the slot has no box shape to cover.
    /// </summary>
    public static ShelfSlotTarget? Cover(Node3D shelf, DeckSlot slot)
    {
        var shapeNode = slot.Area?.GetChildOrNull<CollisionShape3D>(0);
        if (shapeNode?.Shape is not BoxShape3D box)
        {
            ILog.Error($"Shelf slot {slot.Name} has no box trigger, so it gets no click target");
            return null;
        }

        var slotInBox = shapeNode.GlobalTransform.AffineInverse() * slot.GlobalPosition;
        var reach = ReachDownToSurface(box.Size, slotInBox);
        var target = new ShelfSlotTarget { Name = $"{slot.Name}Target", _slot = slot };
        target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = reach.Size } });
        target._highlight = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = reach.Size },
            MaterialOverride = GD.Load<Material>(HighlightMaterialPath),
            Visible = false,
        };
        target.AddChild(target._highlight);
        target.AddToGroup(DeliveryMarker.NoFootprintGroup);
        shelf.AddChild(target);
        target.Transform = shelf.GlobalTransform.AffineInverse() * shapeNode.GlobalTransform
                           * new Transform3D(Basis.Identity, reach.GetCenter());
        return target;
    }

    // The trigger box grown downwards until it ends a little below the slot's origin (given in the box's frame),
    // keeping its top, as a box in that frame.
    private static Aabb ReachDownToSurface(Vector3 triggerSize, Vector3 slotOrigin)
    {
        var top = triggerSize.Y / 2f;
        var bottom = Mathf.Min(-top, slotOrigin.Y - SurfaceMargin);
        var corner = new Vector3(-triggerSize.X / 2f, bottom, -triggerSize.Z / 2f);
        return new Aabb(corner, new Vector3(triggerSize.X, top - bottom, triggerSize.Z));
    }

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        CollisionMask = 0;
        _services?.Get<IStockHand>(hand => _hand = hand);
        RefreshLayer();
    }

    // Subscribed on every entry, not in _Ready, so a target that is reparented (exit, then enter, no second _Ready)
    // keeps hearing its slot.
    public override void _EnterTree()
    {
        if (_slot == null)
            return;
        _slot.CardsChanged -= RefreshLayer;
        _slot.CardsChanged += RefreshLayer;
        if (IsNodeReady())
            RefreshLayer();
    }

    public override void _ExitTree()
    {
        if (_slot != null)
            _slot.CardsChanged -= RefreshLayer;
    }

    // The hand's offer changes without telling the slot (a card drawn, a container picked up), and it decides whether a
    // partial stack is open, so the layer is judged again every physics frame as well.
    public override void _PhysicsProcess(double delta) => RefreshLayer();

    public override void _Notification(int what)
    {
        // A shelf being carried to a new spot is hidden; its free slots must not take clicks at the old spot.
        if (what == NotificationVisibilityChanged)
            RefreshLayer();
    }

    public void Interact()
    {
        if (_slot != null && CanInteract)
            _hand!.TryStock(_slot);
    }

    public void Highlight()
    {
        if (_highlight != null)
            _highlight.Visible = true;
    }

    public void ClearHighlight()
    {
        if (_highlight != null)
            _highlight.Visible = false;
    }

    private void RefreshLayer()
    {
        var open = _slot != null && IsInstanceValid(_slot) && _slot.HasRoom && IsVisibleInTree()
                   && (!_slot.HasCards || (_hand?.Offered is { } offer && _slot.CanTake(offer.Stock)));
        CollisionLayer = open ? InteractableLayerBit : 0;
        if (!open)
            ClearHighlight();
    }
}
