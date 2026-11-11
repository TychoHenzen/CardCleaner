using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     What one <see cref="RestockerScan" /> found: the planner's Godot-free sources and free slots, and the maps
///     from their ids back to the live nodes. Ids come from the scan's <see cref="RestockerIds" />, so a node keeps
///     its id across scans; slot ids and shelf ids are separate id spaces.
/// </summary>
internal sealed class RestockerScanResult
{
    private readonly RestockerIds _sourceIds;
    private readonly RestockerIds _slotIds;
    private readonly RestockerIds _shelfIds;
    private readonly List<StockSource> _sources = new();
    private readonly List<FreeSlot> _slots = new();
    private readonly List<DeckSlot> _rackSlots = new();
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

    /// <summary>The empty slots of the storage racks, where a container that cannot be shelved goes back.</summary>
    public IReadOnlyList<DeckSlot> RackSlots => _rackSlots;

    public void AddSource(CardContainer container, bool onRack, float distance)
    {
        var id = _sourceIds.For(container);
        _sources.Add(new StockSource(id, ShelfPlacementRule.From(container.Kind), onRack, distance));
        _sourceNodes[id] = container;
    }

    public void AddSlot(DeckSlot slot, CardShelf shelf)
    {
        var id = _slotIds.For(slot);
        var shelfId = _shelfIds.For(shelf);
        _slots.Add(new FreeSlot(id, shelfId, shelf.Kind));
        _slotNodes[id] = slot;
        _shelfNodes[shelfId] = shelf;
    }

    public void AddRackSlot(DeckSlot slot) => _rackSlots.Add(slot);

    public Node3D? SourceNode(int id) => Live(_sourceNodes.GetValueOrDefault(id));

    public Node3D? SlotNode(int id) => Live(_slotNodes.GetValueOrDefault(id));

    public Node3D? ShelfNode(int id) => Live(_shelfNodes.GetValueOrDefault(id));

    /// <summary>
    ///     The empty rack slot that takes the container: its origin slot when that one is still empty, else the
    ///     nearest to <paramref name="from" />. Null when no rack slot has room.
    /// </summary>
    public DeckSlot? PutBackSlot(CardContainer container, DeckSlot? origin, Vector3 from)
    {
        var stock = ShelfPlacementRule.From(container.Kind);
        var fitting = _rackSlots.Where(s => ShelfPlacementRule.Accepts(s.Kind, stock)).ToList();
        if (origin != null && fitting.Contains(origin))
            return origin;

        return fitting.OrderBy(s => s.GlobalPosition.DistanceTo(from)).FirstOrDefault();
    }

    /// <summary>Straight-line distance from a source to a slot; huge when either node is gone.</summary>
    public float Distance(int sourceId, int slotId) =>
        SourceNode(sourceId) is { } source && SlotNode(slotId) is { } slot
            ? source.GlobalPosition.DistanceTo(slot.GlobalPosition)
            : float.MaxValue;

    public Vector3? SourcePosition(RestockStep step) => SourceNode(step.SourceId)?.GlobalPosition;

    /// <summary>Where the restocker takes the stock: a slot for a move, the shelf for an unpack.</summary>
    public Vector3? TargetPosition(RestockStep step) =>
        (step.Kind == RestockStepKind.UnpackBox ? ShelfNode(step.TargetId) : SlotNode(step.TargetId))?.GlobalPosition;

    private static Node3D? Live(Node3D? node) => node != null && GodotObject.IsInstanceValid(node) ? node : null;
}
