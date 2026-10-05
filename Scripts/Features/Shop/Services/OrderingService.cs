using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Charges the <see cref="IMoneyService" /> and instances the ordered scene at the delivery marker.
///     Successive orders land in successive grid slots so items never spawn inside each other.
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IOrderingService))]
public partial class OrderingService : Node, IOrderingService
{
    public const int SlotColumns = 3;
    public const int SlotRows = 2;

    /// <summary>Wider than the widest orderable item (the 2.1 m shelf), so neighbours never overlap.</summary>
    public const float SlotSpacingX = 2.3f;

    /// <summary>Deeper than the deepest orderable item (the 1 m shelf).</summary>
    public const float SlotSpacingZ = 1.5f;

    public const float LayerHeight = 1.5f;

    /// <summary>Marker where ordered items appear.</summary>
    [Export]
    public Marker3D? DeliveryPoint { get; set; }

    /// <summary>Node the spawned items are added to. Falls back to the delivery point's parent.</summary>
    [Export]
    public Node? SpawnRoot { get; set; }

    /// <summary>Resolved from the service locator at startup; assignable directly in tests.</summary>
    public IMoneyService? Money { get; set; }

    /// <summary>How many items this service has delivered so far.</summary>
    public int DeliveredCount { get; private set; }

    public override void _Ready()
    {
        ServiceLocator.Get<IMoneyService>(money => Money = money);
    }

    public OrderResult Order(OrderItem item)
    {
        if (item.Scene == null || item.Price <= 0)
            return Fail(item, OrderStatus.InvalidItem);

        var parent = SpawnRoot ?? DeliveryPoint?.GetParent();
        if (DeliveryPoint == null || parent == null || Money == null)
            return Fail(item, OrderStatus.NoDeliveryPoint);

        // Build the item first: an unusable scene must be rejected before the player is charged.
        var built = item.Scene.Instantiate();
        if (built is not Node3D instance)
        {
            built.Free();
            return Fail(item, OrderStatus.InvalidItem);
        }

        if (!Money.TrySpend(item.Price))
        {
            instance.Free();
            return Fail(item, OrderStatus.InsufficientFunds);
        }

        parent.AddChild(instance);
        instance.GlobalPosition = DeliveryPoint.GlobalPosition + SlotOffset(DeliveredCount);
        DeliveredCount++;

        ILog.Print($"Order succeeded: {item.Id} for {item.Price}, balance now {Money.Balance}");
        return new OrderResult(OrderStatus.Success, instance);
    }

    /// <summary>
    ///     Offset from the delivery point for the n-th delivery: a grid centred on the marker, then
    ///     the same grid one layer higher once it is full.
    /// </summary>
    public static Vector3 SlotOffset(int index)
    {
        var perLayer = SlotColumns * SlotRows;
        var layer = index / perLayer;
        var cell = index % perLayer;
        var column = cell % SlotColumns;
        var row = cell / SlotColumns;
        return new Vector3(
            (column - (SlotColumns - 1) / 2f) * SlotSpacingX,
            layer * LayerHeight,
            (row - (SlotRows - 1) / 2f) * SlotSpacingZ);
    }

    private static OrderResult Fail(OrderItem item, OrderStatus status)
    {
        ILog.Print($"Order rejected: {item.Id} ({status})");
        return new OrderResult(status);
    }
}
