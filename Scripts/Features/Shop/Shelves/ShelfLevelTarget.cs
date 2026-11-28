using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using CardCleaner.Scripts.Features.Shop.Ordering;

namespace CardCleaner.Scripts.Features.Shop.Shelves;

/// <summary>
///     The click target of one shelf level: one box over the whole board, its stock included, highlighted as one. The
///     player's hand router decides what a click on it does from the point the aim hit (<see cref="ShelfLevel.PartAt"
///     />); the target itself offers no click (<see cref="CanInteract" /> is false), so without a router it is never
///     used. The box is on the interaction layer while the shelf is shown and the level holds anything or has room, so
///     it stands in front of the stock and a click on any stocked item reaches the level.
/// </summary>
public partial class ShelfLevelTarget : StaticBody3D, IInteractable
{
    private const uint InteractableLayerBit = 4;
    private const float StockReach = 4.5f;
    private const float SurfaceMargin = 0.05f;
    private const string HighlightMaterialPath = "res://Data/Ui/AimHighlightMaterial.tres";

    private ShelfLevel? _level;
    private MeshInstance3D? _highlight;

    /// <summary>The level this target stocks and takes from.</summary>
    public ShelfLevel? Level => _level;

    public bool CanInteract => false;

    public Node3D InteractionBody => this;

    public float InteractionRange => StockReach;

    public string Prompt => string.Empty;

    /// <summary>
    ///     Builds the target for <paramref name="level" /> as a child of <paramref name="shelf" />. It covers the
    ///     trigger boxes of all the level's seats and reaches down to their surface, where the stock lies, so aiming
    ///     anywhere along the board hits it. Null when no seat has a box trigger.
    /// </summary>
    public static ShelfLevelTarget? Cover(Node3D shelf, ShelfLevel level)
    {
        if (BoundsInLevel(level) is not { } bounds)
        {
            ILog.Error($"Shelf level {level.Name} has no seat with a box trigger, so it gets no click target");
            return null;
        }

        var target = new ShelfLevelTarget { Name = $"{level.Name}Target", _level = level };
        target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = bounds.Size } });
        target._highlight = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = bounds.Size },
            MaterialOverride = GD.Load<Material>(HighlightMaterialPath),
            Visible = false,
        };
        target.AddChild(target._highlight);
        target.AddToGroup(DeliveryMarker.NoFootprintGroup);
        shelf.AddChild(target);
        target.Transform = shelf.GlobalTransform.AffineInverse() * level.GlobalTransform
                           * new Transform3D(Basis.Identity, bounds.GetCenter());
        return target;
    }

    // Every seat's trigger box, each grown down to a little below its seat's origin, merged in the level's frame.
    private static Aabb? BoundsInLevel(ShelfLevel level)
    {
        Aabb? bounds = null;
        var toLevel = level.GlobalTransform.AffineInverse();
        foreach (var seat in level.Seats)
        {
            var shapeNode = seat.Area?.GetChildOrNull<CollisionShape3D>(0);
            if (shapeNode?.Shape is not BoxShape3D box)
                continue;

            var half = box.Size / 2f;
            var toBox = toLevel * shapeNode.GlobalTransform;
            var corners = from x in new[] { -half.X, half.X }
                from y in new[] { -half.Y, half.Y }
                from z in new[] { -half.Z, half.Z }
                select toBox * new Vector3(x, y, z);
            var seatBox = corners.Aggregate(new Aabb(toBox.Origin, Vector3.Zero), (b, c) => b.Expand(c));
            var surface = (toLevel * seat.GlobalPosition).Y - SurfaceMargin;
            if (surface < seatBox.Position.Y)
                seatBox = seatBox.Expand(new Vector3(seatBox.Position.X, surface, seatBox.Position.Z));
            bounds = bounds is { } merged ? merged.Merge(seatBox) : seatBox;
        }

        return bounds;
    }

    public override void _Ready()
    {
        CollisionMask = 0;
        RefreshLayer();
    }

    // Subscribed on every entry, not in _Ready, so a target that is reparented keeps hearing its level.
    public override void _EnterTree()
    {
        if (_level == null)
            return;
        _level.Changed -= RefreshLayer;
        _level.Changed += RefreshLayer;
        if (IsNodeReady())
            RefreshLayer();
    }

    public override void _ExitTree()
    {
        if (_level != null)
            _level.Changed -= RefreshLayer;
    }

    public override void _Notification(int what)
    {
        // A shelf being carried to a new spot is hidden; its levels must not take clicks at the old spot.
        if (what == NotificationVisibilityChanged)
            RefreshLayer();
    }

    // The hand router plans every click on a board (it claims this target), so the target itself offers none.
    public void Interact()
    {
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
        var open = _level != null && IsInstanceValid(_level) && IsVisibleInTree()
                   && (_level.Count > 0 || _level.HasRoom);
        CollisionLayer = open ? InteractableLayerBit : 0;
        if (!open)
            ClearHighlight();
    }
}
