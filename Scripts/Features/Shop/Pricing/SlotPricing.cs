using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Packs.Components;
using Godot;
using CardCleaner.Scripts.Features.Shop.Ordering;
using CardCleaner.Scripts.Features.Shop.Shelves;

namespace CardCleaner.Scripts.Features.Shop.Pricing;

/// <summary>
///     Price tag on a shelf: beside one card seat (<see cref="Slot" />) on a card shelf, or under one whole level
///     (<see cref="Level" />) on a product shelf. It prints the asking price of the stock it covers, and a click opens
///     the pricing screen for that stock. Every item asks its own price: a card its
///     <see cref="CardController.ShopPrice" />, a booster or pack its <see cref="CardContainer.ShopPrice" />. On a
///     product-shelf level every item has the same price (<see cref="ShelfPlacementRule.PriceOnPut" />), and setting it
///     prices them all. The price lives with the stock, not the tag, so the tag saves nothing and a price follows its
///     item to any shelf. The tag prints the price of the item a customer takes next (the level's last one). The tag
///     sits on the interaction layer and collides with nothing, so a ray hits it separately from the stock behind it.
/// </summary>
public partial class SlotPricing : StaticBody3D, IInteractable, IDependent
{
    private const uint InteractableLayerBit = 4;
    private const float DefaultInteractionRange = 3.0f;

    private DeckSlot? _slot;
    private ShelfLevel? _level;
    private IServiceResolver? _services;
    private IWholesaleCosts? _wholesale;
    private IPricingScreen? _screen;

    /// <summary>The printed asking price.</summary>
    [Export]
    public Label3D? TagLabel { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>The card seat this tag prices, on a card shelf. Set on the instance root in the shelf scene.</summary>
    [Export]
    public DeckSlot? Slot
    {
        get => _slot;
        set
        {
            UnsubscribeSlot();
            _slot = value;
            SubscribeSlot();
            Refresh();
        }
    }

    /// <summary>The level this tag prices, on a product shelf. Set on the instance root in the shelf scene.</summary>
    [Export]
    public ShelfLevel? Level
    {
        get => _level;
        set
        {
            UnsubscribeSlot();
            _level = value;
            SubscribeSlot();
            Refresh();
        }
    }

    /// <summary>Derived from the current stock on every read, never cached.</summary>
    public int MarketValue => StockMarketValue.For(Stock);

    /// <summary>
    ///     The price a customer pays for the current stock: its set price, else its market price. 0 when the slot
    ///     holds nothing for sale.
    /// </summary>
    public int AskingValue => MarketValue <= 0 ? 0 : Stock switch
    {
        CardController card => card.ShopPrice ?? MarketValue,
        CardContainer container => container.ShopPrice ?? MarketValue,
        _ => MarketValue
    };

    public bool CanInteract => MarketValue > 0;

    public Node3D InteractionBody => this;

    public string Prompt => $"Set price (now {AskingValue})";

    // The item a customer takes next: the level's last one, or the top of the seat's stack.
    private RigidBody3D? Stock =>
        Level != null ? Level.Last : Slot is { Cards.Count: > 0 } slot ? slot.Cards[^1] : null;

    // Everything this tag prices.
    private IReadOnlyList<RigidBody3D> Covered => Level?.Stock ?? Slot?.Cards ?? [];

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        CollisionMask = 0;
        ClearHighlight();
        _services?.Get<IWholesaleCosts>(costs => _wholesale = costs);
        _services?.Get<IPricingScreen>(screen => _screen = screen);
        Refresh();
    }

    public override void _EnterTree()
    {
        // A tag that leaves and re-enters the tree must listen again.
        UnsubscribeSlot();
        SubscribeSlot();
    }

    public override void _ExitTree() => UnsubscribeSlot();

    /// <summary>Opens the pricing screen for the current stock; nothing happens while there is none.</summary>
    public void Interact()
    {
        if (PricedStock() is { } stock)
            _screen?.Open(stock);
    }

    /// <summary>The current stock as the pricing screen prices it, or null when nothing here can be priced.</summary>
    public IPricedStock? PricedStock()
    {
        if (MarketValue <= 0)
            return null;

        return Stock switch
        {
            CardController card => new CardStockPrice(CardName(card), MarketValue, () => card.ShopPrice, price =>
            {
                card.ShopPrice = price;
                Refresh();
            }),
            CardContainer container => ContainerPrice(container),
            _ => null
        };
    }

    // Every container on a level is of one kind (ShelfPlacementRule.LevelTakes), so one price covers the level.
    private ContainerStockPrice ContainerPrice(CardContainer top)
    {
        var stack = Covered;
        return new ContainerStockPrice(top.Kind, stack.Count, MarketValue, _wholesale?.WholesaleFor(top.Kind),
            () => top.ShopPrice, price =>
            {
                foreach (var body in stack)
                {
                    if (body is CardContainer container)
                        container.ShopPrice = price;
                }

                Refresh();
            });
    }

    // "Rare card", or "Rare special card" for a card that shows its signature.
    private static string CardName(CardController card)
    {
        var rarity = SignatureCardHelper.DetermineRarity([card.Signature]);
        return card.Signature.HasMagicalPotential() ? $"{rarity} special card" : $"{rarity} card";
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
    ///     Repaints the printed price from the current stock. Blank when the slot holds nothing for sale.
    /// </summary>
    public void Refresh()
    {
        if (TagLabel != null)
            TagLabel.Text = MarketValue > 0 ? $"{AskingValue}" : string.Empty;
    }

    private void SubscribeSlot()
    {
        if (Slot != null)
            Slot.CardsChanged += Refresh;
        if (Level != null)
            Level.Changed += Refresh;
    }

    private void UnsubscribeSlot()
    {
        if (Slot != null)
            Slot.CardsChanged -= Refresh;
        if (Level != null)
            Level.Changed -= Refresh;
    }
}
