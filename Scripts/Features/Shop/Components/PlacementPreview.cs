using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     What the player sees of a placement session and the physics check behind it: the ghost at the pose, tinted by
///     whether the pose is valid, and whether the physics space blocks the pose. It is a node so the ghost hangs
///     under it in the tree.
/// </summary>
internal sealed partial class PlacementPreview : Node
{
    private FurnitureGhost? _ghost;

    public bool Blocked { get; private set; }

    public void Open(FurnitureItem item)
    {
        Blocked = false;
        if (_ghost == null)
        {
            // ASSUMPTION: the ghost sits under this node, a child of the controller, so its pose is read in the space
            // of the nearest Node3D ancestor, the same space FurnitureRoot places furniture in. Step 7 of the scene
            // test checks they agree.
            _ghost = new FurnitureGhost { Name = "FurnitureGhost" };
            AddChild(_ghost);
        }

        _ghost.Configure(item.Footprint, item.Height);
        _ghost.Visible = true;
    }

    public void Close()
    {
        Blocked = false;
        if (_ghost != null)
            _ghost.Visible = false;
    }

    public void ShowValid(FurniturePose pose) => _ghost?.Show(pose, true);

    public void ShowInvalid(FurniturePose pose) => _ghost?.Show(pose, false);

    /// <summary>
    ///     Asks the physics space about the pose as it is now, so a rotate or aim that arrived after the last physics
    ///     tick is judged on its own clearance. True when the answer changed and the pose needs a refresh.
    /// </summary>
    public bool Probe(FurnitureItem item, FurniturePose pose)
    {
        // A viewport with no 3D world has nothing to collide with.
        var blocked = IsInsideTree() && GetViewport()?.World3D is { } world
                      && FurnitureProbe.IsBlocked(world, item, pose);
        if (blocked == Blocked)
            return false;

        Blocked = blocked;
        return true;
    }
}
