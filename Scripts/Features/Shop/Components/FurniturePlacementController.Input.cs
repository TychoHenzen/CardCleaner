using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The keys that drive a placement session, and the physics probes: the floor pose follows the camera aim while a
///     session is active, and the placed piece under the aim is found while none is.
/// </summary>
public partial class FurniturePlacementController
{
    private IInputService? _input;
    private PlacedFurniture? _aimed;
    private bool _aimOverride;
    private bool _physicsBlocked;

    /// <summary>When true, the camera aim is ignored so a test can place the pose directly.</summary>
    internal bool AimOverride { get => _aimOverride; set => _aimOverride = value; }

    /// <summary>The placed furniture under the camera's aim while no session is active, or null.</summary>
    internal PlacedFurniture? AimedFurniture => _aimed;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IShopExpansion>(expansion => Expansion ??= expansion);
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
        _aimed = IsActive ? null : FindAimed(GetViewport());
        if (IsActive)
            UpdateFromCamera();
    }

    private static PlacedFurniture? FindAimed(Viewport viewport)
    {
        var camera = viewport.GetCamera3D();
        return camera == null ? null : FurnitureProbe.Aimed(viewport.World3D, camera);
    }

    private void TryPickUpAimed()
    {
        if (!IsActive && _aimed != null)
            BeginMove(_aimed);
    }

    private void UpdateFromCamera()
    {
        var camera = GetViewport().GetCamera3D();
        if (camera != null && !_aimOverride
            && FurnitureAim.TryFloorPoint(camera.GlobalPosition, -camera.GlobalBasis.Z, out var x, out var z))
        {
            AimAt(x, z);
        }

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
}
