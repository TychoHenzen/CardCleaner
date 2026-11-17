using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The load half of <see cref="FurnitureSaveService" />: makes the furniture match the records. A whole-layout
///     save keeps each standing piece whose record has its name and item and puts it at the saved pose, so its price
///     tags and stock load onto the same nodes, and frees every piece no record keeps. Boxes are always replaced.
/// </summary>
public partial class FurnitureSaveService
{
    public void Load(NodeSave save)
    {
        if (!save.TryGetProperty<string>(RecordsKey, out var json))
            return;

        if (Furniture == null || Boxes == null || FurnitureRoot == null || BoxRoot == null)
        {
            ILog.Error("FurnitureSaveService is missing an export; furniture was not restored");
            return;
        }

        var records = FurnitureSaveCodec.Decode(json);
        var wholeLayout = save.TryGetProperty<bool>(WholeLayoutKey, out var whole) && whole;
        ClearBoxes();

        // An older save (no whole-layout flag) never listed the scene's starting furniture, so that stays standing
        // and the records are added beside it.
        var standing = wholeLayout ? StandingByName() : new Dictionary<string, PlacedFurniture>();
        var placedRects = wholeLayout ? [] : PlacedFurniture.StandingRects(FurnitureRoot);
        var floor = CurrentFloor();
        foreach (var record in records)
        {
            if (!float.IsFinite(record.X) || !float.IsFinite(record.Z))
            {
                ILog.Error($"Furniture record '{record.Id}' has a non-finite position; skipped");
                continue;
            }

            if (record.IsBox)
                RestoreBox(record);
            else
                RestorePlaced(record, TakeStanding(standing, record), placedRects, floor);
        }

        foreach (var unclaimed in standing.Values)
            Discard(unclaimed);
        Navigation?.Rebake();
    }

    private void ClearBoxes()
    {
        foreach (var box in FurnitureBoxes().ToList())
        {
            box.GetParent()?.RemoveChild(box);
            box.Free();
        }
    }

    private Dictionary<string, PlacedFurniture> StandingByName() =>
        FurnitureRoot!.GetChildren().OfType<PlacedFurniture>().ToDictionary(placed => placed.Name.ToString());

    /// <summary>The standing piece a placed record keeps: same name and same item. It leaves the pool either way.</summary>
    private static PlacedFurniture? TakeStanding(Dictionary<string, PlacedFurniture> standing, FurnitureRecord record)
    {
        if (record.Name is not { Length: > 0 } name || !standing.Remove(name, out var piece))
            return null;
        if (piece.Item?.Id == record.Id)
            return piece;

        // A piece of another kind under the saved name is not the saved piece; the record makes its own.
        Discard(piece);
        return null;
    }

    private static void Discard(PlacedFurniture piece)
    {
        piece.GetParent()?.RemoveChild(piece);
        piece.Free();
    }

    /// <summary>
    ///     The floor the records are judged on. The owned lots come from <see cref="IShopExpansion" />, which is
    ///     restored by its own save entry first: both services load first, and the expansion node sits before this
    ///     one in the Services provider. With the order reversed the lots are not owned yet and a record standing on
    ///     one comes back as its box with an error (<c>FurnitureSaveServiceTest.ARecordOnAnUnownedLotComesBackAsItsBox</c>).
    /// </summary>
    private FurnitureFloorArea CurrentFloor()
    {
        if (_services is { } services && services.Has<IShopExpansion>())
            return ExpansionFloor.For(services.Get<IShopExpansion>());

        ILog.Error("FurnitureSaveService has no IShopExpansion; furniture is restored on the base shop floor");
        return FurnitureFloor.Shop;
    }

    /// <summary>
    ///     Puts the record's piece on the floor: <paramref name="kept" /> moved to the saved pose when the scene
    ///     already has it, else a new piece. A record that is not in the catalog is skipped with an error, and one that
    ///     no longer fits comes back as its box on the delivery area; the kept piece goes either way.
    /// </summary>
    private void RestorePlaced(
        FurnitureRecord record, PlacedFurniture? kept, List<FurnitureRect> placedRects, FurnitureFloorArea floor)
    {
        var item = Furniture!.Find(record.Id);
        if (item == null)
        {
            ILog.Error($"Furniture record '{record.Id}' is not in the furniture catalog; skipped");
            DiscardIfAny(kept);
            return;
        }

        var pose = new FurniturePose(record.X, record.Z, record.QuarterTurns);
        if (!FurniturePlacementModel.IsValid(item.Footprint, pose, placedRects, floor))
        {
            // The player keeps the piece: it was valid when saved, so an item that grew since, or a lot that is not
            // owned (yet), must not delete it. It comes back packed on the delivery area, to be placed again.
            ILog.Error($"Furniture record '{record.Id}' no longer fits the shop floor; it is back in its box");
            DiscardIfAny(kept);
            FurnitureBoxSpawn.OnDeliveryArea(Boxes!, record.Id, BoxRoot!, DeliveryPoint, record.X, record.Z);
            return;
        }

        if (kept != null)
        {
            kept.SetCarried(false);
            kept.Place(pose);
            placedRects.Add(kept.Rect);
            return;
        }

        var placed = PlacedFurniture.Create(item, pose);
        if (placed == null)
        {
            ILog.Error($"Furniture record '{record.Id}' could not be instanced; skipped");
            return;
        }

        placed.StandUnder(FurnitureRoot!, record.Name);
        placedRects.Add(placed.Rect);
    }

    private static void DiscardIfAny(PlacedFurniture? piece)
    {
        if (piece != null)
            Discard(piece);
    }

    // A box that cannot be made is logged by the spawn and skipped.
    private void RestoreBox(FurnitureRecord record) =>
        FurnitureBoxSpawn.At(Boxes!, record.Id, BoxRoot!, record.X, record.Z, DeliveryPoint);
}
