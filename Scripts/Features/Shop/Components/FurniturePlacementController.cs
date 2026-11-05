using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Carries one piece of furniture at a time through the shop: a box being placed from the player's hand, or a
///     placed piece being moved. The ghost shows the pose, and Confirm or Cancel ends the session. The keys and the
///     physics probes live in the partial file.
/// </summary>
[Service(typeof(IFurniturePlacement))]
public partial class FurniturePlacementController : Node, IFurniturePlacement, IDependent
{
    /// <summary>The floor point a new box starts at, before the first aim.</summary>
    public const float StartX = 8f;
    public const float StartZ = 6f;

    private IServiceResolver? _services;
    private FurnitureGhost? _ghost;
    private FurnitureBox? _box;
    private CarriedFurniture? _carried;
    private FurnitureItem? _item;

    [Export]
    public Node3D? FurnitureRoot { get; set; }

    [Export]
    public ShopNavigationRegion? Navigation { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopExpansion? Expansion { get; set; }

    public bool IsActive { get; private set; }

    public FurniturePose CurrentPose { get; private set; }

    public bool CurrentValid { get; private set; }

    public void Inject(IServiceResolver services) => _services = services;

    public bool Begin(FurnitureBox box)
    {
        var item = box.Item;
        if (IsActive || item == null)
            return false;
        if (FurnitureRoot == null)
        {
            ILog.Error($"{Name} has no FurnitureRoot");
            return false;
        }

        _box = box;
        _carried = null;
        _physicsBlocked = false;
        CurrentPose = new FurniturePose(StartX, StartZ, 0);
        StartSession(item);
        Refresh();
        return true;
    }

    public bool BeginMove(PlacedFurniture furniture)
    {
        if (IsActive)
            return false;
        if (!furniture.CanPickUp)
        {
            ILog.Warning("Empty the shelf before moving it.");
            return false;
        }

        var item = furniture.Item;
        if (FurnitureRoot == null || item == null)
        {
            ILog.Error($"{Name} cannot move {furniture.Name}");
            return false;
        }

        _carried = new CarriedFurniture(furniture);
        _box = null;
        _physicsBlocked = false;
        Navigation?.Rebake();
        CurrentPose = _carried.OldPose;
        StartSession(item);
        Refresh();
        return true;
    }

    public void AimAt(float x, float z)
    {
        if (!IsActive)
            return;
        CurrentPose = new FurniturePose(
            FurniturePlacementModel.Snap(x), FurniturePlacementModel.Snap(z), CurrentPose.QuarterTurns);
        Refresh();
    }

    public void Rotate()
    {
        if (!IsActive)
            return;
        CurrentPose = CurrentPose with { QuarterTurns = FurniturePlacementModel.Turn(CurrentPose.QuarterTurns, 1) };
        Refresh();
    }

    public bool Confirm()
    {
        if (!IsActive)
            return false;

        // Rotate and aim only refresh the floor check; the physics check runs on the next tick. Judge the exact
        // pose being committed, so a confirm that follows a rotate within one tick cannot use the old clearance.
        ProbePhysics();
        if (!CurrentValid || !Commit())
            return false;

        Navigation?.Rebake();
        EndSession();
        return true;
    }

    private bool Commit() => _carried != null ? PutDownCarried(_carried) : PlaceNew();

    private bool PutDownCarried(CarriedFurniture carried)
    {
        if (carried.PutDownAt(CurrentPose))
            return true;

        // A load freed the piece while it was carried; there is nothing left to put down.
        EndSession();
        return false;
    }

    private bool PlaceNew()
    {
        var placed = _item == null || FurnitureRoot == null ? null : PlacedFurniture.Create(_item, CurrentPose);
        if (placed == null)
            return false;

        placed.StandUnder(FurnitureRoot!);
        _box?.QueueFree();
        return true;
    }

    public void Cancel()
    {
        if (!IsActive)
            return;
        if (ReleaseCarried())
            Navigation?.Rebake();

        EndSession();
    }

    /// <summary>Puts a carried piece back where it was; false when none is carried or a load freed it.</summary>
    private bool ReleaseCarried() => _carried?.PutBack() == true;

    private void StartSession(FurnitureItem item)
    {
        _item = item;
        IsActive = true;
        if (_ghost == null)
        {
            // ASSUMPTION: the ghost is a child of this Node, so its pose is read in the space of the nearest Node3D
            // ancestor, the same space FurnitureRoot places furniture in. Step 7 of the scene test checks they agree.
            _ghost = new FurnitureGhost { Name = "FurnitureGhost" };
            AddChild(_ghost);
        }

        _ghost.Configure(item.Footprint, item.Height);
        _ghost.Visible = true;
    }

    private void EndSession()
    {
        IsActive = false;
        CurrentValid = false;
        _box = null;
        _carried = null;
        _item = null;
        _physicsBlocked = false;
        if (_ghost != null)
            _ghost.Visible = false;
    }

    private void Refresh()
    {
        if (!IsActive || _item == null)
            return;
        CurrentValid = !_physicsBlocked && FurniturePlacementModel.IsValid(
            _item.Footprint,
            CurrentPose,
            PlacedRects(),
            ExpansionFloor.For(Expansion));
        _ghost?.Show(CurrentPose, CurrentValid);
    }

    private List<FurnitureRect> PlacedRects() => PlacedFurniture.StandingRects(FurnitureRoot);
}
