using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The load half of <see cref="FurnitureSaveService" />: clears the furniture and restores the records.
/// </summary>
public partial class FurnitureSaveService
{
    /// <summary>Height of an unopened box on the shop floor; the boxes are not saved with a height.</summary>
    // ASSUMPTION: boxes sit on the shop floor at a fixed height; no export records a delivery height (Risk Low).
    private const float BoxHeight = 0.5f;

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
        ClearFurniture();

        var placedRects = new List<FurnitureRect>();
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
                RestorePlaced(record, placedRects);
        }

        Navigation?.Rebake();
    }

    private void ClearFurniture()
    {
        foreach (var placed in FurnitureRoot!.GetChildren().OfType<PlacedFurniture>().ToList())
        {
            FurnitureRoot.RemoveChild(placed);
            placed.Free();
        }

        foreach (var box in FurnitureBoxes().ToList())
        {
            box.GetParent()?.RemoveChild(box);
            box.Free();
        }
    }

    private void RestorePlaced(FurnitureRecord record, List<FurnitureRect> placedRects)
    {
        var item = Furniture!.Find(record.Id);
        if (item == null)
        {
            ILog.Error($"Furniture record '{record.Id}' is not in the furniture catalog; skipped");
            return;
        }

        var pose = new FurniturePose(record.X, record.Z, record.QuarterTurns);
        var floor = Expansion is { } expansion ? FurnitureFloor.For(expansion.Plan) : FurnitureFloor.Shop;
        if (!FurniturePlacementModel.IsValid(item.Footprint, pose, placedRects, floor))
        {
            ILog.Error($"Furniture record '{record.Id}' no longer fits the shop floor; skipped");
            return;
        }

        var placed = PlacedFurniture.Create(item, pose);
        if (placed == null)
        {
            ILog.Error($"Furniture record '{record.Id}' could not be instanced; skipped");
            return;
        }

        placed.StandUnder(FurnitureRoot!);
        placedRects.Add(placed.Rect);
    }

    private void RestoreBox(FurnitureRecord record)
    {
        var orderItem = Boxes!.Items.FirstOrDefault(entry => entry.Id == record.Id);
        if (orderItem?.Scene == null)
        {
            ILog.Error($"Furniture box '{record.Id}' is not in the box order catalog; skipped");
            return;
        }

        if (orderItem.Scene.Instantiate() is not Node3D box)
        {
            ILog.Error($"Furniture box '{record.Id}' is not a 3D scene; skipped");
            return;
        }

        BoxRoot!.AddChild(box);
        box.GlobalPosition = new Vector3(record.X, BoxHeight, record.Z);
        // Only OrderingService tracked a box when it was delivered; a restored one must count as an occupant too.
        DeliveryPoint?.Track(box);
    }
}
