using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     What one <see cref="RestockerScan" /> found: the planner's Godot-free sources and free levels
///     (<see cref="FreeSlot" />), and the maps from their ids back to the live nodes. Ids come from the scan's
///     <see cref="RestockerIds" />, so a node keeps its id across scans; level ids and shelf ids are separate id
///     spaces.
/// </summary>
internal sealed class RestockerScanResult
{
    private readonly RestockerIds _sourceIds;
    private readonly RestockerIds _slotIds;
    private readonly RestockerIds _shelfIds;
    private readonly List<StockSource> _sources = new();
    private readonly List<FreeSlot> _slots = new();
    private readonly List<ShelfLevel> _rackLevels = new();
    private readonly Dictionary<int, Node3D> _sourceNodes = new();
    private readonly Dictionary<int, Node3D> _slotNodes = new();
    private readonly Dictionary<int, Node3D> _shelfNodes = new();

    /// <summary>An empty result with ids of its own, for the restocker before its first scan.</summary>
    public RestockerScanResult() : this(new RestockerIds(), new RestockerIds(), new RestockerIds())
    {
    }

    public RestockerScanResult(RestockerIds sourceIds, RestockerIds slotIds, RestockerIds shelfIds)
    {
        _sourceIds = sourceIds;
        _slotIds = slotIds;
        _shelfIds = shelfIds;
    }

    public IReadOnlyList<StockSource> Sources => _sources;

    public IReadOnlyList<FreeSlot> Slots => _slots;

    /// <summary>The storage-rack levels with room, where a container that cannot be shelved goes back.</summary>
    public IReadOnlyList<ShelfLevel> RackLevels => _rackLevels;

    public void AddSource(CardContainer container, bool onRack, float distance)
    {
        var id = _sourceIds.For(container);
        _sources.Add(new StockSource(id, ShelfPlacementRule.From(container.Kind), onRack, distance,
            RestockerScan.ProductsIn(container)));
        _sourceNodes[id] = container;
    }

    public void AddLevel(ShelfLevel level, CardShelf shelf)
    {
        var id = _slotIds.For(level);
        var shelfId = _shelfIds.For(shelf);
        _slots.Add(new FreeSlot(id, shelfId, shelf.Kind, level.Holds, level.EmptySeats));
        _slotNodes[id] = level;
        _shelfNodes[shelfId] = shelf;
    }

    public void AddRackLevel(ShelfLevel level) => _rackLevels.Add(level);

    public Node3D? SourceNode(int id) => Live(_sourceNodes.GetValueOrDefault(id));

    public Node3D? SlotNode(int id) => Live(_slotNodes.GetValueOrDefault(id));

    public Node3D? ShelfNode(int id) => Live(_shelfNodes.GetValueOrDefault(id));

    /// <summary>
    ///     The rack level that takes the container: the level it came from when that one still takes it, else the
    ///     nearest to <paramref name="from" /> that does. Null when no rack level takes it.
    /// </summary>
    public ShelfLevel? PutBackLevel(CardContainer container, ShelfLevel? origin, Vector3 from)
    {
        var stock = ShelfPlacementRule.From(container.Kind);
        var fitting = _rackLevels.Where(level => level.CanTake(stock)).ToList();
        if (origin != null && fitting.Contains(origin))
            return origin;

        return fitting.OrderBy(level => level.GlobalPosition.DistanceTo(from)).FirstOrDefault();
    }

    /// <summary>Straight-line distance from a source to a slot; huge when either node is gone.</summary>
    public float Distance(int sourceId, int slotId) =>
        SourceNode(sourceId) is { } source && SlotNode(slotId) is { } slot
            ? source.GlobalPosition.DistanceTo(slot.GlobalPosition)
            : float.MaxValue;

    /// <summary>Where the restocker walks to pick a source up: in front of its level, or to it when it is loose.</summary>
    public Vector3? SourcePosition(RestockStep step) =>
        SourceNode(step.SourceId) is not { } source ? null
        : source.GetParent()?.GetParent() is ShelfLevel level ? level.StandPoint
        : source.GlobalPosition;

    /// <summary>Where the restocker takes the stock: in front of the level for a move, the shelf for an unpack.</summary>
    public Vector3? TargetPosition(RestockStep step) =>
        step.Kind == RestockStepKind.UnpackBox
            ? ShelfNode(step.TargetId)?.GlobalPosition
            : (SlotNode(step.TargetId) as ShelfLevel)?.StandPoint;

    private static Node3D? Live(Node3D? node) => node != null && GodotObject.IsInstanceValid(node) ? node : null;
}
