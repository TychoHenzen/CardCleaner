using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using CardCleaner.Scripts.Features.Shop.Day;
using CardCleaner.Scripts.Features.Shop.Progression;

namespace CardCleaner.Scripts.Features.Shop.Ordering;

/// <summary>
///     Charges the <see cref="IMoneyService" /> and instances the ordered scene at a <see cref="DeliveryMarker" />.
///     The marker decides where each delivery goes: it tracks its own occupancy, fills its ground slots first, then
///     stacks on the lowest stack. It skips a slot where the item would leave its floor clearance or overlap an
///     occupant, and refuses the order when no slot is left under its ceiling clearance. Each marker keeps its own
///     slots and its own grid (<see cref="DeliveryMarker.Columns" />, <see cref="DeliveryMarker.CellSize" />), so two
///     terminals with different markers never share a grid, and small stock can get cells of its own size.
///     An item is refused as <see cref="OrderStatus.Locked" /> before anything is charged when the shop level is below
///     its unlock level, its id has no unlock row, or no progression service is present.
/// </summary>
[Service(typeof(IOrderingService))]
public partial class OrderingService : Node, IOrderingService, IDependent
{
    private IServiceResolver? _services;

    /// <summary>Where ordered items appear when the ordering terminal names no area of its own.</summary>
    [Export]
    public DeliveryMarker? DeliveryPoint { get; set; }

    /// <summary>Node the spawned items are added to. Falls back to the delivery point's parent.</summary>
    [Export]
    public Node? SpawnRoot { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IMoneyService? Money { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopProgression? Progression { get; set; }

    /// <summary>How many items this service has delivered so far, across every delivery point.</summary>
    public int DeliveredCount { get; private set; }

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IMoneyService>(money => Money = money);
        _services.Get<IShopProgression>(progression => Progression ??= progression);
    }

    public OrderResult Order(OrderItem item, DeliveryMarker? deliveryPoint = null)
    {
        if (item.Scene == null || item.Price <= 0)
            return Fail(item, OrderStatus.InvalidItem);

        if (Progression == null)
        {
            ILog.Warning($"{Name} has no progression service; {item.Id} is refused.");
            return Fail(item, OrderStatus.Locked);
        }

        if (!Progression.IsUnlocked(item.Id))
            return Fail(item, OrderStatus.Locked);

        var point = deliveryPoint ?? DeliveryPoint;
        var parent = ParentFor(point);
        if (point == null || parent == null || Money == null)
            return Fail(item, OrderStatus.NoDeliveryPoint);

        return Deliver(item, item.Scene, point, parent, Money);
    }

    private Node? ParentFor(DeliveryMarker? point) => SpawnRoot ?? point?.GetParent();

    private OrderResult Deliver(OrderItem item, PackedScene scene, DeliveryMarker point, Node parent,
        IMoneyService money)
    {
        // Build the item first: an unusable scene must be rejected before the player is charged, and a
        // delivery area needs its size to find clearance.
        var built = scene.Instantiate();
        if (built is not Node3D instance)
        {
            built.Free();
            return Fail(item, OrderStatus.InvalidItem);
        }

        var placement = point.FindPlacement(instance);
        if (placement is not { } place)
        {
            instance.Free();
            return Fail(item, OrderStatus.DeliveryFull);
        }

        if (!money.TrySpend(item.Price))
        {
            instance.Free();
            return Fail(item, OrderStatus.InsufficientFunds);
        }

        parent.AddChild(instance);
        instance.GlobalPosition = point.GlobalPosition + point.SlotOffset(place.Slot) + Vector3.Up * place.Lift;
        point.Track(instance);
        DeliveredCount++;

        ILog.Print($"Order succeeded: {item.Id} for {item.Price}, balance now {money.Balance}");
        return new OrderResult(OrderStatus.Success, instance);
    }

    private static OrderResult Fail(OrderItem item, OrderStatus status)
    {
        ILog.Print($"Order rejected: {item.Id} ({status})");
        return new OrderResult(status);
    }
}
