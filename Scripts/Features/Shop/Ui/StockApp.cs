using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The Stock app on the backoffice PC. It lists <see cref="Catalog" /> as cards, builds a cart from them and
///     checks the cart out through <see cref="IOrderingService" />. The rules live in <see cref="OrderCart" /> and
///     <see cref="CheckoutPlan" />, the views in <see cref="StockCardGrid" /> and <see cref="StockCartPanel" />;
///     this class wires them to the services and reports the outcome.
/// </summary>
public partial class StockApp : Control, IDependent
{
    private readonly OrderCart _cart = new();
    private readonly Dictionary<string, OrderItem> _items = new();
    private readonly StockCardGrid _cards = new();
    private readonly StockCartPanel _cartPanel;
    private IServiceResolver? _services;
    private IMoneyService? _money;
    private IOrderingService? _ordering;
    private IShopProgression? _progression;
    private OrderCatalog? _catalog;
    private Label _balanceLabel = null!;
    private bool _layoutBuilt;

    public StockApp() => _cartPanel = new StockCartPanel(_cart, () => Level, Checkout);

    /// <summary>
    ///     The items this app sells. Assign another catalog to sell others.
    /// </summary>
    [Export]
    public OrderCatalog? Catalog { get; set; }

    // ASSUMPTION: until IShopProgression resolves, the shop level reads as 0 and no unlock rows are known, so
    // every card shows Unavailable and the grid rebuilds once the progression arrives (Medium risk).
    private int Level => _progression?.Level ?? 0;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        Visible = false;
        EnsureLayout();
        VisibilityChanged += RebuildCards;

        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        ResolveServices(_services);
        if (Catalog != null)
            Populate(Catalog);
        else
            RebuildCards();
    }

    private void ResolveServices(IServiceResolver services)
    {
        services.Get<IOrderingService>(ordering => _ordering = ordering);
        services.Get<IMoneyService>(money =>
        {
            _money = money;
            Subscribe();
            RefreshBalance();
        });
        services.Get<IShopProgression>(progression =>
        {
            _progression = progression;
            Subscribe();
            RebuildCards();
        });
    }

    // _Ready runs once, but the app can leave and re-enter the tree, so its subscriptions are restored
    // on every entry and dropped on every exit.
    public override void _EnterTree() => Subscribe();

    public override void _ExitTree() => Unsubscribe();

    /// <summary>
    ///     Rebuilds the cards from <paramref name="catalog" />.
    /// </summary>
    public void Populate(OrderCatalog catalog)
    {
        EnsureLayout();
        _catalog = catalog;
        _items.Clear();
        foreach (var item in catalog.Items)
            _items[item.Id] = item;
        RebuildCards();
    }

    private void Subscribe()
    {
        if (!IsInsideTree())
            return;

        Unsubscribe();
        if (_money != null)
            _money.BalanceChanged += ShowBalance;
        if (_progression != null)
            _progression.ProgressChanged += RebuildCards;
    }

    private void Unsubscribe()
    {
        if (_money != null)
            _money.BalanceChanged -= ShowBalance;
        if (_progression != null)
            _progression.ProgressChanged -= RebuildCards;
    }

    private void RebuildCards()
    {
        EnsureLayout();
        _cards.Rebuild(_catalog?.Items ?? [], EntryFor, Level, TryAddToCart);
    }

    private StockEntry EntryFor(OrderItem item) =>
        new(item.Id, item.DisplayName, item.Price, _progression?.RequiredLevel(item.Id));

    private void TryAddToCart(StockEntry entry)
    {
        _cart.TryAdd(entry, Level);
        _cartPanel.Refresh();
    }

    private void Checkout()
    {
        var ordering = _ordering;
        var money = _money;
        if (ordering == null || money == null)
        {
            _cartPanel.ShowMessage("Ordering is unavailable right now.");
            return;
        }

        var plan = CheckoutPlan.Evaluate(_cart, money.Balance, Level);
        _cartPanel.ShowMessage(plan.Refusal == CheckoutRefusal.None ? DeliverCart(ordering) : RefusalText(plan));
        _cartPanel.Refresh();
        RefreshBalance();
    }

    // The run is synchronous, so a second press finds the cart already empty. A delivered unit leaves the
    // cart; the first unit that is not delivered stops the run and leaves the rest in the cart.
    private string DeliverCart(IOrderingService ordering)
    {
        var pending = _cart.Lines.Select(line => (Id: line.Entry.Id, Quantity: line.Quantity)).ToList();
        var total = pending.Sum(entry => entry.Quantity);
        var delivered = 0;
        foreach (var (id, quantity) in pending)
        {
            for (var unit = 0; unit < quantity; unit++)
            {
                var status = OrderOne(ordering, id);
                if (status != OrderStatus.Success)
                    return $"Delivered {delivered} of {total}. {StockOutcomeText.Reason(status)}";
                _cart.RemoveOne(id);
                delivered++;
            }
        }
        return "Ordered. Items are at the delivery point.";
    }

    private OrderStatus OrderOne(IOrderingService ordering, string id) =>
        _items.TryGetValue(id, out var item) ? ordering.Order(item).Status : OrderStatus.InvalidItem;

    private string RefusalText(CheckoutPlan plan) => StockOutcomeText.Refusal(plan, NameOf(plan.LockedId));

    private string NameOf(string? id) =>
        _cart.Lines.FirstOrDefault(line => line.Entry.Id == id)?.Entry.Name ?? id ?? string.Empty;

    private void RefreshBalance()
    {
        EnsureLayout();
        ShowBalance(_money?.Balance ?? 0);
    }

    private void ShowBalance(int balance)
    {
        EnsureLayout();
        _balanceLabel.Text = $"Balance: {balance}";
    }

    /// <summary>The catalog half, for tests that read the real controls.</summary>
    internal StockCardGrid CardGrid
    {
        get
        {
            EnsureLayout();
            return _cards;
        }
    }

    /// <summary>The cart half, for tests that read the real controls.</summary>
    internal StockCartPanel CartPanel
    {
        get
        {
            EnsureLayout();
            return _cartPanel;
        }
    }

    /// <summary>Runs the checkout, as the Checkout button does.</summary>
    internal void PressCheckout() => Checkout();

    internal string BalanceText
    {
        get
        {
            EnsureLayout();
            return _balanceLabel.Text;
        }
    }

    // The desktop can populate or query this app before its own _Ready has run, so every entry point builds
    // the layout on first use. This is a deliberate design, not a guess.
    private void EnsureLayout()
    {
        if (_layoutBuilt)
            return;
        _layoutBuilt = true;

        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(column);
        column.AddChild(new Label { Text = "Stock" });
        _balanceLabel = new Label();
        column.AddChild(_balanceLabel);
        _cards.AddTo(column);
        _cartPanel.AddTo(column);
    }
}
