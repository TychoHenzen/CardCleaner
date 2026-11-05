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

    private readonly CheckoutQueue _queue = new();
    private readonly CheckoutItemTray _tray;
    private readonly CheckoutQueueSpots _queueSpots;
    private readonly CheckoutClaim _claim = new();
    private readonly CheckoutStatusLine _status = new();
    private IServiceResolver? _services;

    [Export]
    public Label3D? StatusLabel
    {
        get => _status.Label;
        set => _status.Label = value;
    }

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
        get => _status.Money;
        set => _status.Link(value);
    }

    public bool CanInteract => Session is { State: CheckoutState.AwaitingPayment };

    /// <summary>True while an automated operator holds the front customer's sale.</summary>
    public bool IsClaimed => _claim.IsHeld;

    public Node3D InteractionBody => this;

    /// <summary>Injected at startup through the service resolver; assignable directly in tests.</summary>
    public IShopProgression? Progression { get; set; }

    public void Inject(IServiceResolver services) => _services = services;

    public CheckoutCounter()
    {
        _tray = new CheckoutItemTray(this);
        _queueSpots = new CheckoutQueueSpots(this);
    }

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
        _queueSpots.Find();
        _tray.FindSpots();
        ResolveServices();
        _status.Update();
    }

    // A counter that re-enters the tree listens again and shows the current balance.
    public override void _EnterTree() => _status.Relink();

    public override void _ExitTree() => _status.Unlink();

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
        var claimable = Session is { ScannedCount: 0 } && Front is { Waiting: true };
        return claimable && _claim.TryTake(claimant);
    }

    /// <summary>Gives the claim back. A caller that does not hold the claim releases nothing.</summary>
    public void ReleaseClaim(object claimant)
    {
        ArgumentNullException.ThrowIfNull(claimant);
        _claim.Release(claimant);
    }

    /// <summary>Marks an item of the current basket scanned. False for an unknown, repeated or finished item.</summary>
    public bool Scan(int itemId, object? claimant = null)
    {
        if (!CanOperate(claimant) || Session is not { } session || !session.Scan(itemId))
            return false;

        _tray.MarkScanned(itemId);
        if (session.State == CheckoutState.AwaitingPayment && !_claim.IsHeld)
            PaymentReady?.Invoke(session);

        _status.Say(string.Empty);
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
        if (!IsClaimed && Session is { State: CheckoutState.AwaitingPayment } session)
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

    private void ResolveServices()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IMoneyService>(money => Money = money);
        _services.Get<IShopProgression>(progression => Progression ??= progression);
    }

    // The caller may act on the sale: nobody else holds the claim, and there is a register to act through.
    private bool CanOperate(object? claimant)
    {
        if (!ClaimAllows(claimant))
            return false;

        if (Money != null)
            return true;

        _status.Say(CheckoutPaymentGuard.UnavailableMessage);
        return false;
    }

    private bool ClaimAllows(object? claimant)
    {
        if (!_claim.Excludes(claimant))
            return true;

        _status.Say("The cashier is serving this customer");
        return false;
    }

    private bool TryPay(Func<CheckoutSession, bool> pay, string wrongMessage, object? claimant)
    {
        if (!ClaimAllows(claimant) || PayableSale() is not { } sale)
            return false;

        if (!pay(sale.Session))
        {
            _status.Say(wrongMessage);
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

    // The sale a payment would settle, or null after saying why there is none. A front customer who stopped
    // waiting forfeits the session on the way.
    private CheckoutSale? PayableSale()
    {
        var readiness = CheckoutPaymentGuard.Evaluate(Money != null, Session?.State, Front?.Waiting);
        ReportUnready(readiness);
        return readiness == PaymentReadiness.Ready ? CurrentSale() : null;
    }

    private void ReportUnready(PaymentReadiness readiness)
    {
        if (readiness == PaymentReadiness.CustomerLeft)
        {
            DropDeparted();
            Refresh();
            return;
        }

        if (CheckoutPaymentGuard.MessageFor(readiness) is { } message)
            _status.Say(message);
    }

    private CheckoutSale? CurrentSale() =>
        Session is { } session && Front is { } customer && Money is { } money
            ? new CheckoutSale(session, customer, money)
            : null;

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
        _queue.AssignSpots(_queueSpots.Count, _queueSpots.PositionOf);

        if (Session == null && Front is { } front)
            StartSession(front);

        _status.Update();
    }

    private void StartSession(ICheckoutCustomer customer)
    {
        var items = CheckoutCounterLayout.ItemsOf(customer);
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

        _tray.Lay(items, itemId => Scan(itemId));
    }

    // Ends the session: its props are freed, then listeners hear it and the message is shown.
    private void CloseSession(string message)
    {
        _tray.Clear();
        Session = null;
        _claim.Clear();
        SessionEnded?.Invoke();
        _status.Say(message);
    }
}
