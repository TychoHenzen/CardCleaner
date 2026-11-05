using System;
using System.Diagnostics.CodeAnalysis;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Spawns the day's customers at the street marker on the arrival schedule while the shop is open. Customers
///     already inside when the shop closes finish their visit. The schedule itself is an <see cref="ArrivalDriver" />.
/// </summary>
public partial class CustomerSpawner : Node, IDependent
{
    private const int DefaultCustomersPerDay = 6;
    private const float DefaultSpeed = 1.6f;
    private const float DefaultStuckSeconds = 8f;

    private readonly CustomerBriefing _briefing;
    private readonly ArrivalDriver _arrivals;
    private IServiceResolver? _services;
    private IShopDayService? _dayService;

    public CustomerSpawner()
    {
        _briefing = new CustomerBriefing(GetTree, () => RandomSource);
        _arrivals = new ArrivalDriver(() => RandomSource, () => EffectiveCustomersPerDay, () => SpawnNow());
        _arrivals.Opened += () => SpawnedCount = 0;
    }

    [Export] public PackedScene? CustomerScene { get; set; }

    /// <summary>
    ///     ASSUMPTION (Medium): customers per day before any expansion lot adds its own; levels scale it later
    ///     (#402). Negative counts as 0.
    /// </summary>
    [Export] public int CustomersPerDay { get; set; } = DefaultCustomersPerDay;

    [Export] public float CustomerSpeed { get; set; } = DefaultSpeed;

    [Export] public float CustomerStuckSeconds { get; set; } = DefaultStuckSeconds;

    [Export] public Node3D? SpawnRoot { get; set; }

    [Export] public Marker3D? SpawnMarker { get; set; }

    [Export] public Marker3D? DespawnMarker { get; set; }

    [Export] public Marker3D? CounterMarker { get; set; }

    // ASSUMPTION (Low risk): the composition root registers IRandom, as CardSpawningService already relies on;
    // a node with no injection and no assigned Rng throws on first use.
    /// <summary>The random source for arrival minutes, wishes and rolls; injected, or assigned in tests.</summary>
    public IRandom? Rng { get; set; }

    public IComplaintLog? Complaints { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopExpansion? Expansion { get; set; }

    /// <summary>The customers a day brings: the export plus what the owned expansion lots add.</summary>
    public int EffectiveCustomersPerDay =>
        Math.Max(0, CustomersPerDay) + Math.Max(0, Expansion?.ExtraCustomersPerDay ?? 0);

    public ICheckoutHandoff? Handoff { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopDayService? DayService
    {
        get => _dayService;
        set
        {
            _dayService = value;
            _arrivals.Follow(value);
        }
    }

    /// <summary>Customers spawned since the shop last opened.</summary>
    public int SpawnedCount { get; private set; }

    /// <summary>Scheduled arrivals that have not happened yet.</summary>
    public int PendingArrivals => _arrivals.Pending;

    /// <summary>Offers currently claimed by customers on their way to buy them.</summary>
    public int ClaimedCount => _briefing.ClaimedCount;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services is null)
        {
            if (DayService is null || Complaints is null || Handoff is null)
                ILog.Error($"{Name} was not injected");
            return;
        }

        ServiceFill.Into<IShopDayService>(_services, () => DayService, service => DayService = service);
        ServiceFill.Into<IComplaintLog>(_services, () => Complaints, log => Complaints = log);
        ServiceFill.Into<ICheckoutHandoff>(_services, () => Handoff, handoff => Handoff = handoff);
        ServiceFill.Into<IRandom>(_services, () => Rng, rng => Rng = rng);
        ServiceFill.Into<IShopExpansion>(_services, () => Expansion, expansion => Expansion = expansion);
    }

    public override void _EnterTree() => _arrivals.Attach();

    public override void _ExitTree() => _arrivals.Release();

    /// <summary>
    ///     Spawns one customer now, whether or not the shop is open, and starts its visit. Null (with an error
    ///     log) when the spawner is not fully wired.
    /// </summary>
    public Customer? SpawnNow()
    {
        if (!IsWired())
        {
            ILog.Error($"{Name} is not fully wired; no customer was spawned.");
            return null;
        }

        var customer = CreateCustomer();
        SpawnedCount++;
        _briefing.Send(customer, CounterMarker.GlobalPosition, DespawnMarker.GlobalPosition);
        return customer;
    }

    [MemberNotNullWhen(true, nameof(CustomerScene), nameof(SpawnRoot), nameof(SpawnMarker), nameof(DespawnMarker),
        nameof(CounterMarker), nameof(Handoff), nameof(Complaints))]
    private bool IsWired() => HasPlaces() && CustomerScene is not null && Handoff is not null
        && Complaints is not null && IsInsideTree();

    [MemberNotNullWhen(true, nameof(SpawnRoot), nameof(SpawnMarker), nameof(DespawnMarker), nameof(CounterMarker))]
    private bool HasPlaces() =>
        SpawnRoot is not null && SpawnMarker is not null && DespawnMarker is not null && CounterMarker is not null;

    private IRandom RandomSource =>
        Rng ?? throw new InvalidOperationException($"{Name} has no random source: inject IRandom or assign Rng");

    private Customer CreateCustomer()
    {
        var customer = CustomerScene!.Instantiate<Customer>();
        customer.Speed = CustomerSpeed;
        customer.StuckSeconds = CustomerStuckSeconds;
        customer.Handoff = Handoff;
        customer.Complaints = Complaints;
        SpawnRoot!.AddChild(customer);
        customer.GlobalPosition = SpawnMarker!.GlobalPosition;
        return customer;
    }
}
