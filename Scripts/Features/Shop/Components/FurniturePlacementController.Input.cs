using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The keys that drive a placement session, and the physics probes: the floor pose follows the camera aim while a
///     session is active, and the placed piece under the aim is found while none is.
/// </summary>
public partial class FurniturePlacementController
{
    /// <summary>How far in front of the camera the pick-up probe reaches, in metres.</summary>
    private const float PickUpReach = 4.5f;

    /// <summary>The collision layer that placed furniture and the shop walls sit on.</summary>
    private const uint FurnitureLayerMask = 1;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IInputService>(input =>
        {
            _input = input;
            _input.RegisterAction(this, "furniture_rotate", Key.T, () => Rotate());
            _input.RegisterAction(this, "furniture_confirm", Key.E, () => Confirm());
            _input.RegisterAction(this, "furniture_cancel", Key.C, () => Cancel());
            _input.RegisterAction(this, "furniture_pickup", Key.G, () => TryPickUpAimed());
        });
    }

    public override void _ExitTree() => _input?.UnregisterAllActions(this);

    public override void _PhysicsProcess(double delta)
    {
        if (IsActive)
            UpdateFromCamera();
        else
            UpdateAimedFurniture();
    }

    private void TryPickUpAimed()
    {
        if (!IsActive && AimedFurniture != null)
            BeginMove(AimedFurniture);
    }

    private void UpdateFromCamera()
    {
        var camera = GetViewport().GetCamera3D();
        if (camera != null && !AimOverride
            && FurnitureAim.TryFloorPoint(camera.GlobalPosition, -camera.GlobalBasis.Z, out var x, out var z))
        {
            AimAt(x, z);
        }

        AimedFurniture = null;
        var blocked = IsPhysicallyBlocked();
        if (blocked == _physicsBlocked)
            return;
        _physicsBlocked = blocked;
        Refresh();
    }

    private bool IsPhysicallyBlocked()
    {
        if (_item == null)
            return false;

        var rect = FurniturePlacementModel.RectOf(_item.Footprint, CurrentPose);
        var width = rect.MaxX - rect.MinX - 0.1f;
        var depth = rect.MaxZ - rect.MinZ - 0.1f;
        var height = _item.Height - 0.1f;
        var shape = new BoxShape3D { Size = new Vector3(width, height, depth) };
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, new Vector3(CurrentPose.X, 0.05f + height / 2f, CurrentPose.Z)),
            CollisionMask = FurnitureLayerMask
        };
        var hits = GetViewport().World3D.DirectSpaceState.IntersectShape(query, 1);
        return hits.Count > 0;
    }

    private void UpdateAimedFurniture()
    {
        AimedFurniture = null;
        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return;

        var from = camera.GlobalPosition;
        var to = from - camera.GlobalBasis.Z * PickUpReach;
        var result = GetViewport().World3D.DirectSpaceState.IntersectRay(
            PhysicsRayQueryParameters3D.Create(from, to, FurnitureLayerMask));
        if (result.Count == 0 || result["collider"].Obj is not Node3D hit)
            return;

        Node? node = hit;
        while (node != null && node is not PlacedFurniture)
            node = node.GetParent();
        AimedFurniture = node as PlacedFurniture;
    }
}
