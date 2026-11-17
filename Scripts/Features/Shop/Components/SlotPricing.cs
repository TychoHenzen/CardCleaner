using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Price tag beside one shelf slot. It prints the asking price of the stock in its slot, and a click opens the
///     pricing screen for that stock. A card asks its own price (<see cref="CardController.ShopPrice" />); a booster
///     or pack asks the shop's price for its kind (<see cref="IPriceBook" />). Either way the price lives with the
///     stock, not the tag, so the tag saves nothing. The tag sits on the interaction layer and collides with nothing,
///     so a ray hits it separately from the card in front of it.
/// </summary>
public partial class SlotPricing : StaticBody3D, IInteractable, IDependent
{
    private const uint InteractableLayerBit = 4;
    private const float DefaultInteractionRange = 3.0f;

    private DeckSlot? _slot;
    private IServiceResolver? _services;
    private IPriceBook? _book;
    private IPricingScreen? _screen;

    /// <summary>The printed asking price.</summary>
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
            UnsubscribeSlot();
            _slot = value;
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
        CardContainer container when _book != null => _book.PriceFor(container.Kind),
        _ => MarketValue
    };

    public bool CanInteract => MarketValue > 0;

    public Node3D InteractionBody => this;

    public string Prompt => $"Set price (now {AskingValue})";

    private RigidBody3D? Stock => Slot is { Cards.Count: > 0 } slot ? slot.Cards[0] : null;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        CollisionMask = 0;
        ClearHighlight();
        _services?.Get<IPriceBook>(UseBook);
        _services?.Get<IPricingScreen>(screen => _screen = screen);
        Refresh();
    }

    public override void _EnterTree()
    {
        // A tag that leaves and re-enters the tree must listen again.
        UnsubscribeSlot();
        SubscribeSlot();
        if (_book != null)
        {
            _book.Changed -= Refresh;
            _book.Changed += Refresh;
        }
    }

    public override void _ExitTree()
    {
        UnsubscribeSlot();
        if (_book != null)
            _book.Changed -= Refresh;
    }

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
            CardContainer container when _book != null => new ProductStockPrice(container.Kind, _book),
            _ => null
        };
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

    /// <summary>Repaints the printed price from the current stock. Blank when the slot holds nothing for sale.</summary>
    public void Refresh()
    {
        if (TagLabel != null)
            TagLabel.Text = MarketValue > 0 ? $"{AskingValue}" : string.Empty;
    }

    private void UseBook(IPriceBook book)
    {
        _book = book;
        book.Changed += Refresh;
        Refresh();
    }

    private void SubscribeSlot()
    {
        if (Slot != null)
            Slot.CardsChanged += Refresh;
    }

    private void UnsubscribeSlot()
    {
        if (Slot != null)
            Slot.CardsChanged -= Refresh;
    }
}
