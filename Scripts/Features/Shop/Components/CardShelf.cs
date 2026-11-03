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

        foreach (var slot in Slots) slot.Kind = Kind;
        CollisionLayer |= ContainerHolder.UnpackLayerBit;
    }

    /// <summary>
    ///     Unpacks a box or pack into this shelf's free slots, when the shelf takes the items it holds (a box or pack
    ///     onto a product shelf or storage rack; a card shelf takes none). False when the shelf does not take them,
    ///     so the holder opens the container loose.
    /// </summary>
    public bool TryUnpack(CardContainer container)
    {
        var childKind = CardContainerLayout.ChildKind(container.Kind);
        if (childKind is not { } kind || !ShelfPlacementRule.Accepts(Kind, ShelfPlacementRule.From(kind)))
            return false;

        return container.OpenOnto(Slots.Where(slot => !slot.HasCards).ToList());
    }
}
