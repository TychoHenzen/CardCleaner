using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The checkout counter. Customers queue in front of it and hand over their baskets. The front customer's
///     items are laid on the counter, the player scans each one, then takes payment by card or cash. Payment adds
///     the basket total to <see cref="IMoneyService" /> once and awards shop XP for it once, then the next customer
///     comes forward.
/// </summary>
[Service(typeof(ICheckoutHandoff))]
public partial class CheckoutCounter : StaticBody3D, IInteractable, IDependent, ICheckoutHandoff
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask
    private const string QueuePrefix = "QueueSlot";
    private const string ItemPrefix = "ItemSpot";
    private const string UnavailableMessage = "The register is unavailable right now.";

    private readonly CheckoutQueue _queue = new();
    private List<CheckoutItemProp> _props = new();
    private IServiceResolver? _services;
    private IMoneyService? _money;
    private Marker3D[] _queueSpots = Array.Empty<Marker3D>();
    private Marker3D[] _itemSpots = Array.Empty<Marker3D>();
    private string _message = string.Empty;
    private object? _claimant;

    // A payable sale: the session, the front customer it belongs to, and the money service that takes the payment.
    private readonly record struct Sale(CheckoutSession Session, ICheckoutCustomer Customer, IMoneyService Money);

    [Export]
    public Label3D? StatusLabel { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>The customer at the front of the queue, or null when nobody is waiting.</summary>
    public ICheckoutCustomer? Front => _queue.Front;

    /// <summary>The sale in progress for the front customer, or null when there is none.</summary>
    public CheckoutSession? Session { get; private set; }

    /// <summary>Raised when every item is scanned, and on each interaction while payment is awaited.</summary>
    public event Action<CheckoutSession>? PaymentReady;

    /// <summary>Raised when a session ends, whether it was paid or dropped.</summary>
    public event Action? SessionEnded;

    /// <summary>Injected at startup through the service resolver; assignable directly in tests.</summary>
    public IMoneyService? Money
    {
        get => _money;
        set
        {
            Unsubscribe();
            _money = value;
            Subscribe();
            UpdateLabel();
        }
    }

    public bool CanInteract => Session is { State: CheckoutState.AwaitingPayment };

    /// <summary>True while an automated operator holds the front customer's sale.</summary>
    public bool IsClaimed => _claimant != null;

    public Node3D InteractionBody => this;

    /// <summary>Injected at startup through the service resolver; assignable directly in tests.</summary>
    public IShopProgression? Progression { get; set; }

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
        _queueSpots = CheckoutCounterLayout.MarkersNamed(this, QueuePrefix);
        _itemSpots = CheckoutCounterLayout.MarkersNamed(this, ItemPrefix);
        if (_queueSpots.Length == 0)
            ILog.Error($"{Name} has no {QueuePrefix} markers; customers cannot queue.");
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
        }
        else
        {
            _services.Get<IMoneyService>(money => Money = money);
            _services.Get<IShopProgression>(progression => Progression ??= progression);
        }
        UpdateLabel();
    }

    public override void _EnterTree()
    {
        // A counter that re-enters the tree listens again and shows the current balance.
        Unsubscribe();
        Subscribe();
        UpdateLabel();
    }

    public override void _ExitTree() => Unsubscribe();

    public override void _Process(double delta)
    {
        DropDeparted();
        Refresh();
    }

    /// <summary>Joins the queue. A customer already in line is ignored.</summary>
    public void Checkout(ICheckoutCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (_queue.Add(customer))
            Refresh();
    }

    /// <summary>
    ///     Lets an automated operator (the cashier) serve the front customer. Only a waiting customer whose basket
    ///     nobody has scanned yet can be claimed, and one claim stands at a time.
    /// </summary>
    public bool TryClaim(object claimant)
    {
        ArgumentNullException.ThrowIfNull(claimant);
        if (Session == null || Session.ScannedCount != 0 || _claimant != null || Front is not { Waiting: true })
            return false;

        _claimant = claimant;
        return true;
    }

    /// <summary>Gives the claim back. A caller that does not hold the claim releases nothing.</summary>
    public void ReleaseClaim(object claimant)
    {
        ArgumentNullException.ThrowIfNull(claimant);
        if (ReferenceEquals(claimant, _claimant))
            _claimant = null;
    }

    /// <summary>Marks an item of the current basket scanned. False for an unknown, repeated or finished item.</summary>
    public bool Scan(int itemId, object? claimant = null)
    {
        if (RefusedByClaim(claimant))
            return false;

        if (_money == null)
        {
            SetMessage(UnavailableMessage);
            return false;
        }

        if (Session == null || !Session.Scan(itemId))
            return false;

        _props.FirstOrDefault(prop => prop.ItemId == itemId)?.MarkScanned();
        if (Session.State == CheckoutState.AwaitingPayment && _claimant == null)
            PaymentReady?.Invoke(Session);

        SetMessage(string.Empty);
        return true;
    }

    /// <summary>Takes a card payment of the typed total for the front customer.</summary>
    public bool PayCard(int typedTotal, object? claimant = null) =>
        TryPay(session => session.TryPayCard(typedTotal), "Wrong total", claimant);

    /// <summary>Takes a cash payment with the change picked for the front customer.</summary>
    public bool PayCash(IReadOnlyCollection<int> picked, object? claimant = null) =>
        TryPay(session => session.TryPayCash(picked), "Wrong change", claimant);

    public void Interact()
    {
        // A claimed sale is the cashier's to finish; opening the payment screen would take the player's cursor.
        if (IsClaimed)
            return;

        if (Session is { State: CheckoutState.AwaitingPayment } session)
            PaymentReady?.Invoke(session);
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

    private bool RefusedByClaim(object? claimant)
    {
        if (_claimant == null || ReferenceEquals(claimant, _claimant))
            return false;

        SetMessage("The cashier is serving this customer");
        return true;
    }

    private bool TryPay(Func<CheckoutSession, bool> pay, string wrongMessage, object? claimant)
    {
        if (RefusedByClaim(claimant) || PayableSale() is not { } sale)
            return false;

        if (!pay(sale.Session))
        {
            SetMessage(wrongMessage);
            return false;
        }

        var total = sale.Session.Total;
        sale.Money.Add(total);
        Progression?.AwardCheckout(total);
        sale.Customer.Leave();
        _queue.Remove(sale.Customer);
        CloseSession($"Paid {total}");
        Refresh();
        return true;
    }

    // The guards of a payment: a usable register, a session with a front customer, and every item scanned.
    // A front customer who stopped waiting forfeits the session on the way.
    private Sale? PayableSale()
    {
        if (_money == null)
        {
            SetMessage(UnavailableMessage);
            return null;
        }

        if (Session == null || Front is not { } customer)
            return null;

        if (!customer.Waiting)
        {
            DropDeparted();
            Refresh();
            return null;
        }

        if (Session.State == CheckoutState.Scanning)
        {
            SetMessage("Scan every item first");
            return null;
        }

        return new Sale(Session, customer, _money);
    }

    // A front customer who stopped waiting forfeits the session; any other who stopped leaves the line.
    private void DropDeparted()
    {
        if (Front is { } front && !front.Waiting && Session != null)
            CloseSession("The customer left before paying");

        _queue.RemoveDeparted();
    }

    // Gives each queued customer its spot (extras share the last one), then starts a session for the front one.
    private void Refresh()
    {
        _queue.AssignSpots(_queueSpots.Length, SpotPosition);

        if (Session == null && Front is { } front)
            StartSession(front);

        UpdateLabel();
    }

    private void StartSession(ICheckoutCustomer customer)
    {
        var items = customer.Basket.Items
            .Select((offer, index) => new CheckoutItem(index, offer.AskingPrice))
            .ToList();
        try
        {
            Session = new CheckoutSession(items, customer.Method);
        }
        catch (ArgumentException ex)
        {
            ILog.Warning($"{Name} sent a basket that cannot be checked out: {ex.Message}");
            customer.Leave();
            _queue.Remove(customer);
            Refresh();
            return;
        }

        _props = CheckoutCounterLayout.LayOutItems(this, _itemSpots, items, itemId => Scan(itemId));
    }

    // Ends the session: its props are freed, then listeners hear it and the message is shown.
    private void CloseSession(string message)
    {
        ClearItems();
        Session = null;
        _claimant = null;
        SessionEnded?.Invoke();
        SetMessage(message);
    }

    private void ClearItems()
    {
        _props.ForEach(prop => prop.QueueFree());
        _props.Clear();
    }

    private QueueSpot SpotPosition(int spot)
    {
        var position = _queueSpots[spot].GlobalPosition;
        return new QueueSpot(position.X, position.Y, position.Z);
    }

    private void Subscribe()
    {
        if (_money != null)
            _money.BalanceChanged += OnBalanceChanged;
    }

    private void Unsubscribe()
    {
        if (_money != null)
            _money.BalanceChanged -= OnBalanceChanged;
    }

    private void OnBalanceChanged(int balance) => UpdateLabel();

    private void SetMessage(string message)
    {
        _message = message;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (StatusLabel != null)
            StatusLabel.Text = CheckoutCounterLayout.StatusText(_money, _message);
    }
}
