using Godot;

public partial class FollowCamera : Camera3D
{
    // Path to the Node3D we want to follow
    [Export] public NodePath TargetPath;

    // Cached reference to the target
    private Node3D _target;

    // Offset from the target's position (e.g., above and behind)
    [Export] public Vector3 Offset = new(0, 5, -10);

    // How quickly the camera catches up (higher = snappier)
    [Export] public float Smoothing = 5f;

    public override void _Ready()
    {
        if (!TargetPath.IsEmpty)
            _target = GetNode<Node3D>(TargetPath);
    }

    public override void _Process(double delta)
    {
        if (_target == null) return;

        // Compute where the camera should go this frame
        Vector3 desiredPos = _target.GlobalPosition + Offset;
        GlobalPosition = GlobalPosition.Lerp(desiredPos, Smoothing * (float)delta);

        // Always look at the target
        LookAt(_target.GlobalPosition, Vector3.Up);
    }
}