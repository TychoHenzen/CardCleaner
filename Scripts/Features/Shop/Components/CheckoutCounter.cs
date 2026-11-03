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
///     the basket total to <see cref="IMoneyService" /> once, then the next customer comes forward.
/// </summary>
[Service(typeof(ICheckoutHandoff))]
public partial class CheckoutCounter : StaticBody3D, IInteractable, IDependent, ICheckoutHandoff
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask
    private const string QueuePrefix = "QueueSlot";
    private const string ItemPrefix = "ItemSpot";
    private const string UnavailableMessage = "The register is unavailable right now.";

    private readonly List<ICheckoutCustomer> _queue = new();
    private readonly Dictionary<ICheckoutCustomer, int> _slotOf = new();
    private List<CheckoutItemProp> _props = new();
    private IServiceResolver? _services;
    private IMoneyService? _money;
    private Marker3D[] _queueSpots = Array.Empty<Marker3D>();
    private Marker3D[] _itemSpots = Array.Empty<Marker3D>();
    private string _message = string.Empty;

    [Export]
    public Label3D? StatusLabel { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>The customer at the front of the queue, or null when nobody is waiting.</summary>
    public ICheckoutCustomer? Front => _queue.Count > 0 ? _queue[0] : null;

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

    public Node3D InteractionBody => this;

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
            ILog.Error($"{Name} was not injected");
        else
            _services.Get<IMoneyService>(money => Money = money);
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
        if (_queue.Contains(customer))
            return;

        _queue.Add(customer);
        Refresh();
    }

    /// <summary>Marks an item of the current basket scanned. False for an unknown, repeated or finished item.</summary>
    public bool Scan(int itemId)
    {
        if (_money == null)
        {
            SetMessage(UnavailableMessage);
            return false;
        }

        if (Session == null || !Session.Scan(itemId))
            return false;

        _props.FirstOrDefault(prop => prop.ItemId == itemId)?.MarkScanned();
        if (Session.State == CheckoutState.AwaitingPayment)
            PaymentReady?.Invoke(Session);

        SetMessage(string.Empty);
        return true;
    }

    /// <summary>Takes a card payment of the typed total for the front customer.</summary>
    public bool PayCard(int typedTotal) => TryPay(session => session.TryPayCard(typedTotal), "Wrong total");

    /// <summary>Takes a cash payment with the change picked for the front customer.</summary>
    public bool PayCash(IReadOnlyCollection<int> picked) =>
        TryPay(session => session.TryPayCash(picked), "Wrong change");

    public void Interact()
    {
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

    private bool TryPay(Func<CheckoutSession, bool> pay, string wrongMessage)
    {
        if (_money == null)
        {
            SetMessage(UnavailableMessage);
            return false;
        }

        if (Session == null || Front is not { } customer)
            return false;

        if (!customer.Waiting)
        {
            DropDeparted();
            Refresh();
            return false;
        }

        if (Session.State == CheckoutState.Scanning)
        {
            SetMessage("Scan every item first");
            return false;
        }

        if (!pay(Session))
        {
            SetMessage(wrongMessage);
            return false;
        }

        var total = Session.Total;
        _money.Add(total);
        customer.Leave();
        Dequeue(customer);
        CloseSession($"Paid {total}");
        Refresh();
        return true;
    }

    // A front customer who stopped waiting forfeits the session; any other who stopped leaves the line.
    private void DropDeparted()
    {
        if (Front is { } front && !front.Waiting && Session != null)
            CloseSession("The customer left before paying");

        foreach (var customer in _queue.Where(customer => !customer.Waiting).ToList())
            Dequeue(customer);
    }

    // Gives each queued customer its spot (extras share the last one), then starts a session for the front one.
    private void Refresh()
    {
        for (var i = 0; i < _queue.Count && _queueSpots.Length > 0; i++)
        {
            var customer = _queue[i];
            var spot = Math.Min(i, _queueSpots.Length - 1);
            if (_slotOf.TryGetValue(customer, out var slot) && slot == spot)
                continue;

            _slotOf[customer] = spot;
            var position = _queueSpots[spot].GlobalPosition;
            customer.QueueAt(new QueueSpot(position.X, position.Y, position.Z));
        }

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
            Dequeue(customer);
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
        SessionEnded?.Invoke();
        SetMessage(message);
    }

    private void ClearItems()
    {
        _props.ForEach(prop => prop.QueueFree());
        _props.Clear();
    }

    private void Dequeue(ICheckoutCustomer customer)
    {
        _queue.Remove(customer);
        _slotOf.Remove(customer);
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
