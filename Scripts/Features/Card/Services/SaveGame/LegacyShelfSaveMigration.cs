using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using Godot;
using Saveable;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
///     Brings a save made before the shop's starting product shelf and storage rack became furniture onto the
///     current layout. Such a save keeps those shelves' stock at their old seats, and it is recognised by its price
///     tag entries, which old tags saved under their node paths. A container saved in an old seat moves to the same
///     slot's seat on the moved shelf, where that slot locks it again on load. The tag entries themselves are left
///     alone: tags save nothing now (prices live with the stock), so the load ignores them.
/// </summary>
internal static class LegacyShelfSaveMigration
{
    private const string PriceIdPrefix = "slot_price:";
    private const string GameSaveId = "game_save_service";
    private const string ContainersKey = "containers";

    // How far a saved container may lie from an old seat and still count as that slot's stock.
    private const float SeatMatchMeters = 0.25f;

    // The old slots seated stock at their own position plus this offset, which did not turn with the slot.
    private static readonly Vector3 OldSeatOffset = new(0f, 0.16f, 0.2f);

    // Both shelves stood unturned in the old layout; each slot origin is in the shelf's frame, as its scene had it.
    private static readonly MovedShelf[] Shelves =
    [
        new("World/Storefront/ProductShelf", "World/Furniture/ProductShelf/Content", new Vector3(10f, 0f, 10.5f),
            [new Vector3(-0.7f, 0.9f, -0.2f), new Vector3(0f, 0.9f, -0.2f), new Vector3(0.7f, 0.9f, -0.2f)]),
        new("World/RightStorage/StorageRack", "World/Furniture/StorageRack/Content", new Vector3(20f, 0f, -6.5f),
            [
                new Vector3(-0.55f, 0.2f, -0.2f), new Vector3(0.55f, 0.2f, -0.2f),
                new Vector3(-0.55f, 1.1f, -0.2f), new Vector3(0.55f, 1.1f, -0.2f)
            ]),
    ];

    /// <summary>
    ///     Returns <paramref name="payload" /> moved onto the current layout when it holds a price id under an old
    ///     shelf path, finding the moved slots under <paramref name="root" />. Any other payload comes back as it is.
    /// </summary>
    internal static string Migrate(string payload, Node root)
    {
        var save = SaveExtension.DeserializeObject<TreeSave>(payload);
        if (save == null || !save.Collections.Keys.Any(IsOldShelfPriceId))
            return payload;

        MoveShelvedContainers(save, root);
        ILog.Print("Moved a save from before the starting shelves became furniture onto the current layout");
        return SaveExtension.SerializeObject(save);
    }

    // A price id under an old shelf path can only come from a save made before the move.
    private static bool IsOldShelfPriceId(string key) =>
        key.StartsWith(PriceIdPrefix, StringComparison.Ordinal)
        && Shelves.Any(shelf => key.Contains($"/{shelf.OldPath}/", StringComparison.Ordinal));

    private static void MoveShelvedContainers(TreeSave save, Node root)
    {
        if (!save.TryGetCollection(GameSaveId, out var node) || node == null
            || !node.TryGetProperty<List<object>>(ContainersKey, out var raw) || raw == null)
            return;

        node.SetOrAddProperty(ContainersKey, SavedContainers.Relocate(raw, position => NewSeatFor(position, root)));
    }

    private static Vector3? NewSeatFor(Vector3 position, Node root)
    {
        foreach (var shelf in Shelves)
        {
            for (var i = 0; i < shelf.SlotOrigins.Length; i++)
            {
                var oldSeat = shelf.OldOrigin + shelf.SlotOrigins[i] + OldSeatOffset;
                if (position.DistanceTo(oldSeat) <= SeatMatchMeters)
                    return root.GetNodeOrNull<DeckSlot>($"{shelf.NewPath}/Slot{i + 1}")?.SeatPosition(0);
            }
        }

        return null;
    }

    /// <summary>A starting shelf's node path and pose before the move, and its node path after it.</summary>
    private sealed record MovedShelf(string OldPath, string NewPath, Vector3 OldOrigin, Vector3[] SlotOrigins);
}
