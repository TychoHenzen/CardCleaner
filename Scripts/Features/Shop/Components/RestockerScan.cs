using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Gathers what the restocker can carry and where it can go: unopened containers on a storage rack or lying
///     loose near a shelf, and the empty slots of the sales shelves. Containers the player holds or the restocker
///     already carries are not sources.
/// </summary>
internal sealed class RestockerScan
{
    private const string ContainerGroup = "card_containers";
    private const string HandName = "Hand";

    private readonly SceneTree _tree;
    private readonly float _pickupRadius;

    public RestockerScan(SceneTree tree, float pickupRadius)
    {
        _tree = tree;
        _pickupRadius = pickupRadius;
    }

    public RestockerScanResult Gather(Vector3 from)
    {
        var result = new RestockerScanResult();
        var sales = Shelves(CardShelf.SalesGroupName);
        AddSources(result, from, sales.Concat(Shelves(CardShelf.StorageGroupName)).ToList());
        AddSlots(result, sales);
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
        if (container.IsOpened || container.IsHeld || parent?.Name.ToString() == HandName)
            return null;
        if (parent is DeckSlot slot)
            return slot.GetParent() is CardShelf { IsStorage: true } ? true : null;
        if (container.Freeze)
            return null;
        return shelves.Any(shelf => shelf.GlobalPosition.DistanceTo(container.GlobalPosition) <= _pickupRadius)
            ? false
            : null;
    }

    private static void AddSlots(RestockerScanResult result, IReadOnlyList<CardShelf> sales)
    {
        for (var shelfId = 0; shelfId < sales.Count; shelfId++)
        {
            foreach (var slot in sales[shelfId].Slots.Where(s => !s.HasCards))
                result.AddSlot(slot, shelfId, sales[shelfId]);
        }
    }
}
