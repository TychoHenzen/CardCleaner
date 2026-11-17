using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>One item on its way out of its wrapper: frozen, and moved along its <see cref="RisePath" />.</summary>
internal sealed class RisingItem
{
    private readonly Node3D _node;
    private readonly Node? _parent;
    private readonly Transform3D _target;
    private readonly RisePath _path;

    public RisingItem(Node3D node, int index, RisePath path)
    {
        _node = node;
        _parent = node.GetParent();
        _target = node.GlobalTransform;
        _path = path;
        Index = index;
    }

    public int Index { get; }

    // The player may take an item while it rises, or a load may clear it away; then it is no longer ours to move.
    public bool StillOurs() =>
        GodotObject.IsInstanceValid(_node) && _node.IsInsideTree() && _node.GetParent() == _parent;

    public void Hold()
    {
        if (_node is RigidBody3D body)
            body.Freeze = true;
        MoveTo(0f);
    }

    public void MoveTo(float t) => _node.GlobalTransform = _path.At(t, _target);

    public void LetGo()
    {
        _node.GlobalTransform = _target;
        Unfreeze();
    }

    public void Unfreeze()
    {
        if (_node is RigidBody3D body)
            body.Freeze = false;
    }
}
