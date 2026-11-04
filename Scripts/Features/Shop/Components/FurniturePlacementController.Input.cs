using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The keys that drive a placement session, and the physics probes: the floor pose follows the camera aim while a
///     session is active, and the placed piece under the aim is found while none is.
/// </summary>
public partial class FurniturePlacementController
{
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

    public override void _ExitTree()
    {
        _input?.UnregisterAllActions(this);
        // A controller leaving the tree mid-move puts the carried piece back, so it is never left hidden.
        ReleaseCarried();
    }

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
        ProbePhysics();
    }

    /// <summary>
    ///     Queries the physics space for the pose as it is now, so a rotate or aim that arrived after the last
    ///     physics tick is judged on its own clearance and not on the previous pose's.
    /// </summary>
    private void ProbePhysics()
    {
        // A viewport with no 3D world has nothing to collide with.
        var blocked = _item != null && IsInsideTree() && GetViewport()?.World3D is { } world
                      && FurnitureProbe.IsBlocked(world, _item, CurrentPose);
        if (blocked == _physicsBlocked)
            return;
        _physicsBlocked = blocked;
        Refresh();
    }

    private void UpdateAimedFurniture()
    {
        var camera = GetViewport().GetCamera3D();
        AimedFurniture = camera == null ? null : FurnitureProbe.Aimed(GetViewport().World3D, camera);
    }
}
