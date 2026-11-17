using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A shelf that displays stock in its slots. Each slot is a one-card <see cref="DeckSlot" /> child:
///     a body dropped on a slot locks and shows there, and picking it up releases it again. The
///     <see cref="Kind" /> decides which stock the slots take and which group the shelf joins. A shelf is also an
///     <see cref="IUnpackTarget" />: a carried box or pack pointed at it unpacks into its free slots.
/// </summary>
public partial class CardShelf : RigidBody3D, IUnpackTarget
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

        CollisionLayer |= ContainerHolder.UnpackLayerBit;
        AddAimVolume();
    }

    /// <summary>The box over this shelf's whole bounds that the hand aims at to unpack onto it.</summary>
    public Area3D? AimVolume => GetNodeOrNull<Area3D>(AimVolumeName);

    private const string AimVolumeName = "UnpackAim";

    // The shelf's colliders follow its model's parts, so a ray aimed between two boards passes through them. One
    // trigger box over the bounds of those colliders, on the unpack layer only, lets the hand aim at the whole shelf.
    // It is a trigger, not a body: the navigation bake parses static bodies by their layer, so a body here would keep
    // a carried shelf (whose own layers are off) in the mesh. It is in DeliveryMarker.FootprintGroup, so a delivered
    // shelf stacks as its bounding box, the way PlacedFurniture's footprint box stands for a placed one. Built here
    // with no owner, so no scene ever stores it.
    private void AddAimVolume()
    {
        var shapes = GetChildren().OfType<CollisionShape3D>().ToList();
        if (shapes.Count == 0 || AimVolume != null)
            return;

        var bounds = shapes.Skip(1).Aggregate(BoundsOf(shapes[0]), (box, shape) => box.Merge(BoundsOf(shape)));
        var area = new Area3D
        {
            Name = AimVolumeName,
            CollisionLayer = ContainerHolder.UnpackLayerBit,
            CollisionMask = 0,
            Monitoring = false
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
    ///     Unpacks a box or pack into this shelf's free slots, when the shelf takes the items it holds (a box or pack
    ///     onto a product shelf or storage rack; a card shelf takes none). False when the shelf does not take them,
    ///     so the holder opens the container loose.
    /// </summary>
    public bool TryUnpack(CardContainer container)
    {
        if (!TakesContentsOf(container))
            return false;

        return container.OpenOnto(Slots.Where(slot => !slot.HasCards).ToList());
    }

    public bool CanUnpack(CardContainer container) =>
        TakesContentsOf(container) && Slots.Any(slot => !slot.HasCards);

    private bool TakesContentsOf(CardContainer container) =>
        CardContainerLayout.ChildKind(container.Kind) is { } kind
        && ShelfPlacementRule.Accepts(Kind, ShelfPlacementRule.From(kind));
}
