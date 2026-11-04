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
    private IInputService? _input;
    private FurnitureGhost? _ghost;
    private FurnitureBox? _box;
    private PlacedFurniture? _moving;
    private FurniturePose _oldPose;
    private FurnitureItem? _item;
    private bool _physicsBlocked;

    [Export]
    public Node3D? FurnitureRoot { get; set; }

    [Export]
    public ShopNavigationRegion? Navigation { get; set; }

    public bool IsActive { get; private set; }

    public FurniturePose CurrentPose { get; private set; }

    public bool CurrentValid { get; private set; }

    /// <summary>When true, the camera aim is ignored so a test can place the pose directly.</summary>
    internal bool AimOverride { get; set; }

    /// <summary>The placed furniture under the camera's aim while no session is active, or null.</summary>
    internal PlacedFurniture? AimedFurniture { get; private set; }

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
        _moving = null;
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

        _oldPose = furniture.Pose;
        _moving = furniture;
        _box = null;
        _physicsBlocked = false;
        // The piece stays in the tree, hidden and off the collision layer, so a save or a quit during the move
        // still finds it at its old pose.
        furniture.SetCarried(true);
        Navigation?.Rebake();
        CurrentPose = _oldPose;
        StartSession(item);
        Refresh();
        return true;
    }

    public void AimAt(float x, float z)
    {
        if (!IsActive)
            return;
        var quarterTurns = CurrentPose.QuarterTurns;
        CurrentPose = new FurniturePose(
            FurniturePlacementModel.Snap(x),
            FurniturePlacementModel.Snap(z),
            quarterTurns);
        Refresh();
    }

    public void Rotate()
    {
        if (!IsActive)
            return;
        var quarterTurns = FurniturePlacementModel.Turn(CurrentPose.QuarterTurns, 1);
        CurrentPose = CurrentPose with { QuarterTurns = quarterTurns };
        Refresh();
    }

    public bool Confirm()
    {
        var root = FurnitureRoot;
        if (!IsActive || !CurrentValid || root == null || _item == null)
            return false;

        if (_moving != null)
        {
            if (!GodotObject.IsInstanceValid(_moving))
            {
                // A load freed the piece while it was carried; there is nothing left to put down.
                EndSession();
                return false;
            }

            _moving.Place(CurrentPose);
            _moving.SetCarried(false);
        }
        else
        {
            var placed = PlacedFurniture.Create(_item, CurrentPose);
            if (placed == null)
                return false;
            root.AddChild(placed);
            _box?.QueueFree();
        }

        Navigation?.Rebake();
        EndSession();
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

    /// <summary>
    ///     Puts a carried piece back down at the pose it was picked up from. False when nothing was carried, or
    ///     when a load freed the piece meanwhile.
    /// </summary>
    private bool ReleaseCarried()
    {
        if (_moving == null || !GodotObject.IsInstanceValid(_moving))
            return false;
        _moving.Place(_oldPose);
        _moving.SetCarried(false);
        return true;
    }

    private void StartSession(FurnitureItem item)
    {
        _item = item;
        IsActive = true;
        _ghost ??= CreateGhost();
        _ghost.Configure(item.Footprint, item.Height);
        _ghost.Visible = true;
    }

    // ASSUMPTION: the ghost is a child of this Node, so its pose is read in the space of the nearest Node3D
    // ancestor, the same space FurnitureRoot places furniture in. Step 7 of the scene test checks the two agree.
    private FurnitureGhost CreateGhost()
    {
        var ghost = new FurnitureGhost { Name = "FurnitureGhost" };
        AddChild(ghost);
        return ghost;
    }

    private void EndSession()
    {
        IsActive = false;
        CurrentValid = false;
        _box = null;
        _moving = null;
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
            FurnitureFloor.Shop);
        _ghost?.Show(CurrentPose, CurrentValid);
    }

    private List<FurnitureRect> PlacedRects()
    {
        var rects = new List<FurnitureRect>();
        if (FurnitureRoot == null)
            return rects;
        foreach (var child in FurnitureRoot.GetChildren())
        {
            if (child is PlacedFurniture { Item: not null, IsCarried: false } placed)
                rects.Add(placed.Rect);
        }

        return rects;
    }
}
