using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A shelf that displays stock in its slots. Each slot is a <see cref="DeckSlot" /> child that holds one kind of
///     stock (one card, one pack, or a stack of boosters; see <see cref="ShelfPlacementRule" />): a body dropped on a
///     slot locks and shows there, and picking it up releases it again. The
///     <see cref="Kind" /> decides which stock the slots take and which group the shelf joins. The restocker unpacks a
///     cardboard box into its free slots (<see cref="TryUnpack" />).
/// </summary>
public partial class CardShelf : RigidBody3D
{
    /// <summary>Scene-tree group of every product shelf and card shelf, for shop code that lists sales stock.</summary>
    public const string SalesGroupName = "sales_shelves";

    /// <summary>Scene-tree group of every storage rack.</summary>
    public const string StorageGroupName = "storage_racks";

    private const ShelfKind DefaultKind = ShelfKind.CardShelf;

    /// <summary>What this shelf is: a card shelf, a product shelf or a storage rack.</summary>
    [Export] public ShelfKind Kind { get; set; } = DefaultKind;

    /// <summary>True for a storage rack.</summary>
    public bool IsStorage => Kind == ShelfKind.StorageRack;

    /// <summary>The slots of this shelf, in scene order.</summary>
    public IEnumerable<DeckSlot> Slots => GetChildren().OfType<DeckSlot>();

    /// <summary>Total number of slots.</summary>
    public int SlotCount => Slots.Count();

    /// <summary>Stock currently shown on the shelf.</summary>
    public IEnumerable<RigidBody3D> StockedCards => Slots.SelectMany(slot => slot.Cards);

    public int StockedCount => StockedCards.Count();

    /// <summary>The price tags of this shelf, one sibling per slot.</summary>
    public IEnumerable<SlotPricing> Pricings => GetChildren().OfType<SlotPricing>();

    /// <summary>The price tag that prices <paramref name="slot" />, or null when the shelf has none.</summary>
    public SlotPricing? PricingFor(DeckSlot slot) => Pricings.FirstOrDefault(pricing => pricing.Slot == slot);

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() == nameof(Kind) || base._PropertyCanRevert(property);
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() == nameof(Kind)
            ? (int)DefaultKind
            : base._PropertyGetRevert(property);
    }

    public override void _Ready()
    {
        AddToGroup(IsStorage ? StorageGroupName : SalesGroupName);

        foreach (var slot in Slots.ToList())
        {
            slot.Kind = Kind;
            ShelfSlotTarget.Cover(this, slot);
        }

        AddFootprintVolume();
    }

    /// <summary>The box over this shelf's whole bounds, on no physics layer.</summary>
    public Area3D? FootprintVolume => GetNodeOrNull<Area3D>(FootprintVolumeName);

    private const string FootprintVolumeName = "ShelfFootprint";

    // The shelf's colliders follow its model's parts, so they leave the space between two boards open. One trigger
    // box over the bounds of those colliders is in DeliveryMarker.FootprintGroup, so a delivered shelf stacks as its
    // bounding box, the way PlacedFurniture's footprint box stands for a placed one. The delivery check reads shapes
    // from the tree, so the box is on no layer and no ray or body ever meets it. It is a trigger, not a body: the
    // navigation bake parses static bodies, so a body here would keep a carried shelf in the mesh. Built here with no
    // owner, so no scene ever stores it.
    private void AddFootprintVolume()
    {
        var shapes = GetChildren().OfType<CollisionShape3D>().ToList();
        if (shapes.Count == 0 || FootprintVolume != null)
            return;

        var bounds = shapes.Skip(1).Aggregate(BoundsOf(shapes[0]), (box, shape) => box.Merge(BoundsOf(shape)));
        var area = new Area3D
        {
            Name = FootprintVolumeName,
            CollisionLayer = 0,
            CollisionMask = 0,
            Monitoring = false,
            Monitorable = false
        };
        area.AddToGroup(DeliveryMarker.FootprintGroup);
        area.AddChild(new CollisionShape3D
            { Position = bounds.GetCenter(), Shape = new BoxShape3D { Size = bounds.Size } });
        AddChild(area);
    }

    // In the shelf's own frame, from the shape's local transform, so a turned shelf still gets a box along its sides.
    private static Aabb BoundsOf(CollisionShape3D shape) => shape.Transform * shape.Shape.GetDebugMesh().GetAabb();

    /// <summary>The click targets of this shelf's slots, one per slot.</summary>
    public IEnumerable<ShelfSlotTarget> SlotTargets => GetChildren().OfType<ShelfSlotTarget>();

    /// <summary>
    ///     Unpacks a cardboard box into this shelf's free slots: every item a free slot takes moves in, the last in
    ///     first, and the box stays a box with the rest inside. False, with nothing moved, when no slot takes any of
    ///     its items or the container is not a cardboard box (a booster box or booster pack is opened in the hand).
    /// </summary>
    public bool TryUnpack(CardContainer container) =>
        container.Storage is { } storage && storage.MoveInto(Slots.ToList()) > 0;
}
