using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     What one <see cref="RestockerScan" /> found: the planner's Godot-free sources and free slots, and the maps
///     from their ids back to the live nodes. Ids are assigned in scene-tree order; slot ids and shelf ids are
///     separate id spaces.
/// </summary>
internal sealed class RestockerScanResult
{
    private readonly List<StockSource> _sources = new();
    private readonly List<FreeSlot> _slots = new();
    private readonly Dictionary<int, Node3D> _sourceNodes = new();
    private readonly Dictionary<int, Node3D> _slotNodes = new();
    private readonly Dictionary<int, Node3D> _shelfNodes = new();

    public IReadOnlyList<StockSource> Sources => _sources;

    public IReadOnlyList<FreeSlot> Slots => _slots;

    public void AddSource(CardContainer container, bool onRack, float distance)
    {
        var id = _sources.Count;
        _sources.Add(new StockSource(id, ShelfPlacementRule.From(container.Kind), onRack, distance));
        _sourceNodes[id] = container;
    }

    public void AddSlot(DeckSlot slot, int shelfId, CardShelf shelf)
    {
        var id = _slots.Count;
        _slots.Add(new FreeSlot(id, shelfId, shelf.Kind));
        _slotNodes[id] = slot;
        _shelfNodes[shelfId] = shelf;
    }

    public Node3D? SourceNode(int id) => Live(_sourceNodes.GetValueOrDefault(id));

    public Node3D? SlotNode(int id) => Live(_slotNodes.GetValueOrDefault(id));

    public Node3D? ShelfNode(int id) => Live(_shelfNodes.GetValueOrDefault(id));

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
