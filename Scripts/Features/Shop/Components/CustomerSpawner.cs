using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Spawns the day's customers at the street marker on the arrival schedule while the shop is open. Customers
///     already inside when the shop closes finish their visit.
/// </summary>
public partial class CustomerSpawner : Node, IDependent
{
    private const int DefaultCustomersPerDay = 6;
    private const float DefaultSpeed = 1.6f;
    private const float DefaultStuckSeconds = 8f;

    private readonly HashSet<string> _claimed = new(StringComparer.Ordinal);
    private IReadOnlyList<int> _schedule = Array.Empty<int>();
    private IServiceResolver? _services;
    private IShopDayService? _dayService;
    private int _next;

    [Export] public PackedScene? CustomerScene { get; set; }

    /// <summary>
    ///     ASSUMPTION (Medium): customers per day; levels and expansions scale it later (#402, #406).
    ///     Negative counts as 0.
    /// </summary>
    [Export] public int CustomersPerDay { get; set; } = DefaultCustomersPerDay;

    [Export] public float CustomerSpeed { get; set; } = DefaultSpeed;

    [Export] public float CustomerStuckSeconds { get; set; } = DefaultStuckSeconds;

    [Export] public Node3D? SpawnRoot { get; set; }

    [Export] public Marker3D? SpawnMarker { get; set; }

    [Export] public Marker3D? DespawnMarker { get; set; }

    [Export] public Marker3D? CounterMarker { get; set; }

    /// <summary>
    ///     The random source for arrival minutes, wishes and rolls. Injected through the service resolver at
    ///     startup; tests assign a seeded one directly.
    /// </summary>
    public IRandom? Rng { get; set; }

    public IComplaintLog? Complaints { get; set; }

    public ICheckoutHandoff? Handoff { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopDayService? DayService
    {
        get => _dayService;
        set
        {
            Unsubscribe();
            _dayService = value;
            Subscribe();
        }
    }

    /// <summary>Customers spawned since the shop last opened.</summary>
    public int SpawnedCount { get; private set; }

    /// <summary>Scheduled arrivals that have not happened yet.</summary>
    public int PendingArrivals => _schedule.Count - _next;

    /// <summary>Offers currently claimed by customers on their way to buy them.</summary>
    public int ClaimedCount => _claimed.Count;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services is null)
        {
            if (DayService is null || Complaints is null || Handoff is null)
                ILog.Error($"{Name} was not injected");
            return;
        }

        ResolveServices(_services);
    }

    private void ResolveServices(IServiceResolver services)
    {
        services.Get<IShopDayService>(service => DayService ??= service);
        services.Get<IComplaintLog>(log => Complaints ??= log);
        services.Get<ICheckoutHandoff>(handoff => Handoff ??= handoff);
        services.Get<IRandom>(rng => Rng ??= rng);
    }

    public override void _EnterTree()
    {
        Unsubscribe();
        Subscribe();
    }

    public override void _ExitTree() => Unsubscribe();

    /// <summary>
    ///     Spawns one customer now, whether or not the shop is open, and starts its visit. Null (with an error
    ///     log) when the spawner is not fully wired.
    /// </summary>
    public Customer? SpawnNow()
    {
        if (CustomerScene is null || SpawnRoot is null || SpawnMarker is null || DespawnMarker is null
            || CounterMarker is null || Handoff is null || Complaints is null || !IsInsideTree())
        {
            ILog.Error($"{Name} is not fully wired; no customer was spawned.");
            return null;
        }

        var customer = CustomerScene.Instantiate<Customer>();
        customer.Speed = CustomerSpeed;
        customer.StuckSeconds = CustomerStuckSeconds;
        customer.Handoff = Handoff;
        customer.Complaints = Complaints;
        SpawnRoot.AddChild(customer);
        customer.GlobalPosition = SpawnMarker.GlobalPosition;

        var offers = new ShelfOfferIndex(GetTree());
        var wish = ShoppingPlanner.DrawWish(RandomSource);
        var plan = ShoppingPlanner.Plan(offers.Collect(), _claimed, wish, RandomSource);
        var claims = plan.Where(pick => pick.Outcome.Buys).Select(pick => pick.Offer.OfferId).ToList();
        foreach (var id in claims)
            _claimed.Add(id);

        customer.Finished += _ =>
        {
            foreach (var id in claims)
                _claimed.Remove(id);
        };
        SpawnedCount++;
        customer.Begin(plan, offers, CounterMarker.GlobalPosition, DespawnMarker.GlobalPosition);
        return customer;
    }

    private void OnOpened()
    {
        SpawnedCount = 0;
        _next = 0;
        _schedule = Array.Empty<int>();
        var service = _dayService!;
        if (service.ClosingMinute - service.OpeningMinute <= ArrivalSchedule.LastEntryMarginMinutes)
        {
            ILog.Error($"{Name}: the shop is open too short a time for any arrival.");
            return;
        }

        _schedule = ArrivalSchedule.Build(
            service.OpeningMinute, service.ClosingMinute, Math.Max(0, CustomersPerDay), RandomSource);
    }

    // ASSUMPTION (Low risk): the composition root registers IRandom, as CardSpawningService already relies on;
    // a node with no injection and no assigned Rng throws on first use.
    private IRandom RandomSource
    {
        get
        {
            return Rng ?? throw new InvalidOperationException(
                $"{Name} has no random source: inject IRandom or assign Rng");
        }
    }

    private void OnTimeChanged(int day, int minute)
    {
        if (_dayService is not { IsOpen: true })
            return;

        while (_next < _schedule.Count && _schedule[_next] <= minute)
        {
            _next++;
            SpawnNow();
        }
    }

    private void OnClosed(DayReport report) => ClearSchedule();

    private void OnRestored() => ClearSchedule();

    private void ClearSchedule()
    {
        _schedule = Array.Empty<int>();
        _next = 0;
    }

    private void Subscribe()
    {
        if (_dayService is null)
            return;

        _dayService.Opened += OnOpened;
        _dayService.Closed += OnClosed;
        _dayService.Restored += OnRestored;
        _dayService.TimeChanged += OnTimeChanged;
    }

    private void Unsubscribe()
    {
        if (_dayService is null)
            return;

        _dayService.Opened -= OnOpened;
        _dayService.Closed -= OnClosed;
        _dayService.Restored -= OnRestored;
        _dayService.TimeChanged -= OnTimeChanged;
    }
}
