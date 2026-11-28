using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using Godot;
using CardCleaner.Scripts.Features.Shop.Ordering;
using CardCleaner.Scripts.Features.Shop.Pricing;

namespace CardCleaner.Scripts.Features.Shop.Shelves;

/// <summary>
///     A shelf that displays stock on its levels. Each board is a <see cref="ShelfLevel" /> child whose seats
///     (<see cref="DeckSlot" />s) hold one kind of stock (cards, packs, or stacks of boosters; see
///     <see cref="ShelfPlacementRule" />): a body dropped on a board locks into the level's next free seat, and a click
///     on the board stocks or takes for the whole level (<see cref="ShelfLevelTarget" />). The <see cref="Kind" />
///     decides which stock the levels take and which group the shelf joins. The restocker unpacks a cardboard box onto
///     its levels (<see cref="TryUnpack" />).
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

    /// <summary>The levels (boards) of this shelf, in scene order.</summary>
    public IEnumerable<ShelfLevel> Levels => GetChildren().OfType<ShelfLevel>();

    /// <summary>Every seat of this shelf, level by level.</summary>
    public IEnumerable<DeckSlot> Slots => Levels.SelectMany(level => level.Seats);

    /// <summary>Total number of seats.</summary>
    public int SlotCount => Slots.Count();

    /// <summary>Stock currently shown on the shelf.</summary>
    public IEnumerable<RigidBody3D> StockedCards => Slots.SelectMany(slot => slot.Cards);

    public int StockedCount => StockedCards.Count();

    /// <summary>
    ///     The price tags of this shelf: one per seat on a card shelf, one per level on a product shelf, none on a
    ///     storage rack.
    /// </summary>
    public IEnumerable<SlotPricing> Pricings => GetChildren().OfType<SlotPricing>();

    /// <summary>
    ///     The price tag that prices the stock on <paramref name="slot" />: its own tag on a card shelf, its level's on
    ///     a product shelf. Null when the shelf has none.
    /// </summary>
    public SlotPricing? PricingFor(DeckSlot slot) =>
        Pricings.FirstOrDefault(pricing => pricing.Slot == slot)
        ?? Pricings.FirstOrDefault(pricing => pricing.Level != null && pricing.Level == slot.GetParent());

    /// <summary>
    ///     The price tag that prices what a customer takes next from <paramref name="level" />: the tag of its last
    ///     card's seat on a card shelf, the level's own tag on a product shelf. Null when there is none.
    /// </summary>
    public SlotPricing? PricingFor(ShelfLevel level) =>
        Pricings.FirstOrDefault(pricing => pricing.Level == level)
        ?? (level.Last?.GetParent() is DeckSlot seat ? PricingFor(seat) : null);

    /// <summary>The level holding <paramref name="slot" />, or null when it is no seat of this shelf.</summary>
    public ShelfLevel? LevelOf(DeckSlot slot) =>
        slot.GetParent() is ShelfLevel level && level.GetParent() == this ? level : null;

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

        foreach (var level in Levels.ToList())
        {
            level.Configure(Kind);
            ShelfLevelTarget.Cover(this, level);
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

    /// <summary>The click targets of this shelf's levels, one per level.</summary>
    public IEnumerable<ShelfLevelTarget> LevelTargets => GetChildren().OfType<ShelfLevelTarget>();

    /// <summary>
    ///     Unpacks a cardboard box onto this shelf's levels: every item a level takes moves in, the last in first, and
    ///     the box stays a box with the rest inside. False, with nothing moved, when no level takes any of its items or
    ///     the container is not a cardboard box (a booster box or booster pack is opened in the hand).
    /// </summary>
    public bool TryUnpack(CardContainer container) =>
        container.Storage is { } storage && storage.MoveInto(Levels.ToList<IItemStore>()) > 0;
}
