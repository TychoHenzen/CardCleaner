using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Charges the <see cref="IMoneyService" /> and instances the ordered scene at a <see cref="DeliveryMarker" />.
///     The marker decides where each delivery goes: it tracks its own occupancy, fills its ground slots first, then
///     stacks on the lowest stack. It skips a slot where the item would leave its floor clearance or overlap an
///     occupant, and refuses the order when no slot is left under its ceiling clearance. Each marker keeps its own
///     slots, so two terminals with different markers never share a grid.
/// </summary>
[Service(typeof(IOrderingService))]
public partial class OrderingService : Node, IOrderingService, IDependent
{
    public const int SlotColumns = 3;
    public const int SlotRows = 2;

    /// <summary>
    ///     Fits the 2.1 m shelf in one column. A wider footprint, such as an arcade cabinet with its holders, takes the
    ///     next slot that clears every shape.
    /// </summary>
    public const float SlotSpacingX = 2.3f;

    /// <summary>
    ///     Fits the 1 m deep shelf in one row. A deeper footprint takes the next slot that clears every shape.
    /// </summary>
    public const float SlotSpacingZ = 1.5f;

    private const int SlotCount = SlotColumns * SlotRows;

    private IServiceResolver? _services;

    /// <summary>Where ordered items appear when the ordering terminal names no area of its own.</summary>
    [Export]
    public DeliveryMarker? DeliveryPoint { get; set; }

    /// <summary>Node the spawned items are added to. Falls back to the delivery point's parent.</summary>
    [Export]
    public Node? SpawnRoot { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IMoneyService? Money { get; set; }

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
    }

    public OrderResult Order(OrderItem item, DeliveryMarker? deliveryPoint = null)
    {
        if (item.Scene == null || item.Price <= 0)
            return Fail(item, OrderStatus.InvalidItem);

        var point = deliveryPoint ?? DeliveryPoint;
        var parent = SpawnRoot ?? point?.GetParent();
        if (point == null || parent == null || Money == null)
            return Fail(item, OrderStatus.NoDeliveryPoint);

        // Build the item first: an unusable scene must be rejected before the player is charged, and a
        // delivery area needs its size to find clearance.
        var built = item.Scene.Instantiate();
        if (built is not Node3D instance)
        {
            built.Free();
            return Fail(item, OrderStatus.InvalidItem);
        }

        var placement = point.FindPlacement(SlotCount, SlotOffset, new Vector2(SlotSpacingX, SlotSpacingZ), instance);
        if (placement is not { } place)
        {
            instance.Free();
            return Fail(item, OrderStatus.DeliveryFull);
        }

        if (!Money.TrySpend(item.Price))
        {
            instance.Free();
            return Fail(item, OrderStatus.InsufficientFunds);
        }

        parent.AddChild(instance);
        instance.GlobalPosition = point.GlobalPosition + SlotOffset(place.Slot) + Vector3.Up * place.Lift;
        point.Track(instance);
        DeliveredCount++;

        ILog.Print($"Order succeeded: {item.Id} for {item.Price}, balance now {Money.Balance}");
        return new OrderResult(OrderStatus.Success, instance);
    }

    /// <summary>
    ///     Offset from the delivery point of slot <paramref name="index" />: a grid centred on the marker.
    /// </summary>
    public static Vector3 SlotOffset(int index)
    {
        var column = index % SlotColumns;
        var row = index / SlotColumns;
        return new Vector3(
            (column - (SlotColumns - 1) / 2f) * SlotSpacingX,
            0f,
            (row - (SlotRows - 1) / 2f) * SlotSpacingZ);
    }

    private static OrderResult Fail(OrderItem item, OrderStatus status)
    {
        ILog.Print($"Order rejected: {item.Id} ({status})");
        return new OrderResult(status);
    }
}
