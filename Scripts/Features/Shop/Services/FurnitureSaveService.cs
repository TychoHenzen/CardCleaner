using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Saves the shop's placed furniture and its unopened boxes into the save slot, and restores them on load.
///     The save half lives here; <c>FurnitureSaveService.Restore.cs</c> holds the load half. It is an ISaveable node in
///     the shop's Services provider and carries no [Service] attribute, because nothing resolves it by type.
/// </summary>
public partial class FurnitureSaveService : Node, ILoadsFirst, IDependent
{
    public const string RecordsKey = "records";

    private IServiceResolver? _services;

    [Export] public FurnitureCatalog? Furniture { get; set; }

    [Export] public OrderCatalog? Boxes { get; set; }

    [Export] public Node3D? FurnitureRoot { get; set; }

    [Export] public Node3D? BoxRoot { get; set; }

    [Export] public ShopNavigationRegion? Navigation { get; set; }

    /// <summary>The delivery area that restored boxes count against, so a new order avoids them.</summary>
    [Export] public DeliveryMarker? DeliveryPoint { get; set; }

    public StringName UniqueID => "furniture";

    public void Inject(IServiceResolver services) => _services = services;

    public void Save(NodeSave save)
    {
        var records = new List<FurnitureRecord>();
        if (FurnitureRoot != null)
        {
            foreach (var placed in FurnitureRoot.GetChildren().OfType<PlacedFurniture>())
            {
                if (placed.Item is not { } item)
                    continue;
                var pose = placed.Pose;
                records.Add(new FurnitureRecord(item.Id, pose.X, pose.Z, pose.QuarterTurns, false));
            }
        }

        foreach (var box in FurnitureBoxes())
        {
            if (box.Item is not { } boxItem)
                continue;
            var at = box.GlobalPosition;
            records.Add(new FurnitureRecord(boxItem.Id, at.X, at.Z, 0, true));
        }

        save.SetOrAddProperty(RecordsKey, FurnitureSaveCodec.Encode(records));
    }

    /// <summary>Every furniture box in the group while this node is in the tree; none outside it.</summary>
    private IEnumerable<FurnitureBox> FurnitureBoxes() =>
        IsInsideTree()
            ? GetTree().GetNodesInGroup(FurnitureBox.GroupName).OfType<FurnitureBox>()
            : Enumerable.Empty<FurnitureBox>();
}
