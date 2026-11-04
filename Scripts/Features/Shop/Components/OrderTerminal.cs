using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A <see cref="ScreenTerminal" /> whose screen is an ordering overlay
///     (<see cref="Ui" />) for <see cref="Catalog" />. Opening and closing,
///     the player lock, the cursor claim and Escape come from the base.
/// </summary>
public partial class OrderTerminal : ScreenTerminal
{
    private IMoneyService? _money;
    private IOrderingService? _ordering;

    /// <summary>
    ///     Items this terminal sells. Assign another catalog to sell others.
    /// </summary>
    [Export]
    public OrderCatalog? Catalog { get; set; }

    /// <summary>
    ///     Where this terminal's orders appear. Null falls back to the
    ///     ordering service's own delivery point.
    /// </summary>
    [Export]
    public DeliveryMarker? DeliveryPoint { get; set; }

    [Export]
    public OrderTerminalUi? Ui { get; set; }

    /// <summary>
    ///     Outcome of the latest order, or null when none was placed.
    /// </summary>
    public OrderResult? LastOrder { get; private set; }

    public override bool CanInteract => !IsOpen && Catalog != null && Ui != null;

    protected override bool CanOpen => Ui != null;

    public override void _Ready()
    {
        base._Ready();

        if (Catalog == null || Ui == null)
        {
            ILog.Error($"{Name} needs a Catalog and a Ui assigned");
            return;
        }

        Ui.Populate(Catalog);
        Subscribe();

        var services = Services;
        if (services == null)
            return;

        services.Get<IOrderingService>(ordering => _ordering = ordering);
        services.Get<IMoneyService>(money =>
        {
            _money = money;
            Subscribe();
            Ui.SetBalance(_money.Balance);
        });
    }

    // _Ready runs once, but a terminal can leave and re-enter the tree, so the
    // subscriptions that _ExitTree drops are restored on every entry.
    public override void _EnterTree() => Subscribe();

    public override void _ExitTree()
    {
        base._ExitTree();
        Unsubscribe();
    }

    protected override void OnOpening()
    {
        if (Ui == null)
            return;

        if (_money != null)
            Ui.SetBalance(_money.Balance);
        Ui.ShowMessage(string.Empty);
        Ui.Visible = true;
    }

    protected override void OnClosing()
    {
        if (Ui != null)
            Ui.Visible = false;
    }

    private void Subscribe()
    {
        if (Ui == null || !IsInsideTree())
            return;

        Unsubscribe();
        Ui.OrderRequested += OnOrderRequested;
        Ui.CloseRequested += Close;
        if (_money != null)
            _money.BalanceChanged += Ui.SetBalance;
    }

    private void Unsubscribe()
    {
        if (Ui == null)
            return;

        Ui.OrderRequested -= OnOrderRequested;
        Ui.CloseRequested -= Close;
        if (_money != null)
            _money.BalanceChanged -= Ui.SetBalance;
    }

    private void OnOrderRequested(OrderItem item)
    {
        LastOrder = null;
        if (_ordering == null || Ui == null)
        {
            Ui?.ShowMessage("Ordering is unavailable right now.");
            return;
        }

        var result = _ordering.Order(item, DeliveryPoint);
        LastOrder = result;
        Ui.ShowMessage(result.Status switch
        {
            OrderStatus.Success =>
                $"Ordered {item.DisplayName}. It is at the delivery point.",
            OrderStatus.InsufficientFunds =>
                $"Not enough money for {item.DisplayName}.",
            OrderStatus.DeliveryFull =>
                "The delivery area is full. There is no room left for another "
                + "item.",
            OrderStatus.Locked =>
                $"{item.DisplayName} is locked. Reach a higher shop level "
                + "to order it.",
            _ => $"{item.DisplayName} cannot be ordered right now."
        });
    }
}
