using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using Godot;
using CardCleaner.Scripts.Features.Shop.Shelves;

namespace CardCleaner.Scripts.Features.Shop.Restocking;

/// <summary>
///     Gathers what the restocker can carry and where it can go: unopened containers on a storage rack or lying
///     loose near a shelf, and the levels of the sales shelves with room (empty ones and part-filled ones, which take
///     only more of what they hold). Containers the player holds or the restocker already carries are not sources.
/// </summary>
internal sealed class RestockerScan
{
    private const string ContainerGroup = "card_containers";
    private const string HandName = "Hand";

    private readonly SceneTree _tree;
    private readonly float _pickupRadius;
    private readonly RestockerIds _sourceIds = new();
    private readonly RestockerIds _slotIds = new();
    private readonly RestockerIds _shelfIds = new();

    public RestockerScan(SceneTree tree, float pickupRadius)
    {
        _tree = tree;
        _pickupRadius = pickupRadius;
    }

    /// <summary>
    ///     Scans the shop. While the restocker carries a container, that container is the only source: it must be
    ///     shelved or put back before anything else is taken. A carried cardboard box with no booster box or booster
    ///     left in it is no source at all, so the plan is idle and the box goes back to a rack.
    /// </summary>
    public RestockerScanResult Gather(Vector3 from, CardContainer? carried = null)
    {
        var result = new RestockerScanResult(_sourceIds, _slotIds, _shelfIds);
        var sales = Shelves(CardShelf.SalesGroupName);
        var storage = Shelves(CardShelf.StorageGroupName);
        if (carried != null)
        {
            if (ProductsIn(carried) > 0)
                result.AddSource(carried, false, 0f);
        }
        else
            AddSources(result, from, sales.Concat(storage).ToList());
        AddLevels(result, sales);
        foreach (var level in storage.SelectMany(rack => rack.Levels).Where(level => level.HasRoom))
            result.AddRackLevel(level);
        return result;
    }

    private List<CardShelf> Shelves(string group) => _tree.GetNodesInGroup(group).OfType<CardShelf>().ToList();

    private void AddSources(RestockerScanResult result, Vector3 from, IReadOnlyList<CardShelf> shelves)
    {
        foreach (var container in _tree.GetNodesInGroup(ContainerGroup).OfType<CardContainer>())
        {
            if (Classify(container, shelves) is { } onRack)
                result.AddSource(container, onRack, from.DistanceTo(container.GlobalPosition));
        }
    }

    // True: locked on a rack. False: loose near a shelf. Null: not restockable.
    private bool? Classify(CardContainer container, IReadOnlyList<CardShelf> shelves)
    {
        var parent = container.GetParent();
        if (container.IsOpened || container.IsHeld || parent?.Name.ToString() == HandName || ProductsIn(container) == 0)
            return null;
        if (parent is DeckSlot seat)
            return seat.GetParent() is ShelfLevel { } level && level.GetParent() is CardShelf { IsStorage: true }
                ? true
                : null;
        if (container.Freeze)
            return null;
        return shelves.Any(shelf => shelf.GlobalPosition.DistanceTo(container.GlobalPosition) <= _pickupRadius)
            ? false
            : null;
    }

    /// <summary>
    ///     How many items in <paramref name="container" /> a product shelf takes: 1 for a booster box or booster, and
    ///     for a cardboard box the booster boxes and boosters it holds. Cards stay in a box, and an empty box waits for
    ///     the player to throw it away, so a box is worth a trip only while this is above 0.
    /// </summary>
    public static int ProductsIn(CardContainer container) =>
        container.Storage is not { } storage
            ? 1
            : storage.CountOf(item => ShelfPlacementRule.From(item) is StockKind.Pack or StockKind.Booster);

    // Every sales level with room: an empty one, or a part-filled one, which the restocker tops up.
    private static void AddLevels(RestockerScanResult result, IReadOnlyList<CardShelf> sales)
    {
        foreach (var shelf in sales)
        {
            foreach (var level in shelf.Levels.Where(level => level.HasRoom))
                result.AddLevel(level, shelf);
        }
    }
}
