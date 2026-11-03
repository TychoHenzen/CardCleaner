using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Price tag beside one shelf slot. It shows the asking price of the stock in its slot and steps the markup on
///     interaction. The tag sits on the interaction layer and collides with nothing, so a ray hits it separately
///     from the card in front of it.
/// </summary>
public partial class SlotPricing : StaticBody3D, IInteractable, ISaveable
{
    private const uint InteractableLayerBit = 4;
    private const float DefaultInteractionRange = 3.0f;
    private const string MarkupKey = "markup";

    private DeckSlot? _slot;

    [Export]
    public Label3D? TagLabel { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>The slot this tag prices. Set on the instance root in the shelf scene.</summary>
    [Export]
    public DeckSlot? Slot
    {
        get => _slot;
        set
        {
            Unsubscribe();
            _slot = value;
            Subscribe();
            Refresh();
        }
    }

    public int MarkupPercent { get; private set; } = AskingPrice.DefaultMarkupPercent;

    /// <summary>Derived from the current stock on every read, never cached.</summary>
    public int MarketValue => StockMarketValue.For(Slot is { Cards.Count: > 0 } slot ? slot.Cards[0] : null);

    public int AskingValue => MarketValue <= 0 ? 0 : AskingPrice.For(MarketValue, MarkupPercent);

    public bool CanInteract => MarketValue > 0;

    public Node3D InteractionBody => this;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        CollisionMask = 0;
        ClearHighlight();
        Refresh();
    }

    public override void _EnterTree()
    {
        // A tag that leaves and re-enters the tree must listen again.
        Unsubscribe();
        Subscribe();
    }

    public override void _ExitTree() => Unsubscribe();

    // The id is built from the node path, so each tag keeps its own id across a restart. Outside the tree the
    // path does not exist yet, so the name stands in; the tree walk only visits nodes that are in the tree.
    public StringName UniqueID => IsInsideTree() ? $"slot_price:{GetPath()}" : $"slot_price:{Name}";

    public void Save(NodeSave save) => save.SetOrAddProperty(MarkupKey, MarkupPercent);

    public void Load(NodeSave save)
    {
        // Read as object: a typed read of a corrupt value throws, and that would stop the rest of the tree load.
        var percent = AskingPrice.DefaultMarkupPercent;
        if (save.TryGetProperty<object>(MarkupKey, out var raw) && raw is long value
            && value >= int.MinValue && value <= int.MaxValue)
            percent = (int)value;
        SetMarkup(percent);
    }

    public void Interact() => SetMarkup(AskingPrice.NextMarkup(MarkupPercent));

    public void SetMarkup(int percent)
    {
        MarkupPercent = AskingPrice.Sanitise(percent);
        Refresh();
    }

    public void Highlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = true;
    }

    public void ClearHighlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = false;
    }

    /// <summary>
    ///     Repaints the label from the current stock. Empty when the slot has nothing with a market price.
    /// </summary>
    public void Refresh()
    {
        if (TagLabel != null)
            TagLabel.Text = MarketValue > 0 ? AskingPrice.Readout(MarketValue, MarkupPercent) : string.Empty;
    }

    private void Subscribe()
    {
        if (Slot != null)
            Slot.CardsChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (Slot != null)
            Slot.CardsChanged -= Refresh;
    }
}
