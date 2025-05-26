using System.Collections.Generic;
using Godot;

[Tool]
public partial class ConveyorBelt : StaticBody3D
{
    [Export] public NodePath AreaPath { get; set; } = "Area3D";
    [Export] public Vector3 Direction { get; set; } = new(1, 0, 0);
    [Export] public float Speed { get; set; } = 1.0f;

    private Area3D _detectionArea;
    private readonly List<RigidBody3D> _bodiesOnBelt = new();

    public override void _Ready()
    {
        GD.Print($"[ConveyorBelt:{Name}] _Ready called");
        SetPhysicsProcess(true);

        _detectionArea = GetNodeOrNull<Area3D>(AreaPath);
        if (_detectionArea != null)
        {
            // Debug the area’s state
            GD.Print($"[ConveyorBelt:{Name}] Found Area3D at '{AreaPath}'; " +
                     $"Monitoring={_detectionArea.Monitoring}, Mask={_detectionArea.CollisionMask}");

            // Ensure it’s listening
            _detectionArea.Monitoring = true;

            _detectionArea.BodyEntered += body => OnBodyEntered(body as RigidBody3D);
            _detectionArea.BodyExited  += body => OnBodyExited(body as RigidBody3D);
        }
        else
        {
            GD.PrintErr($"[ConveyorBelt:{Name}] ERROR: Could not find Area3D at '{AreaPath}'");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Optional debug: show how many bodies the area currently overlaps:
        var overlaps = _detectionArea?.GetOverlappingBodies().Count ?? 0;
        if (overlaps > 0)
            GD.Print($"[ConveyorBelt:{Name}] Overlapping bodies: {overlaps}");

        // Push only the ones we’ve registered
        var push = Direction.Normalized() * Speed;
        foreach (var body in _bodiesOnBelt)
        {
            if (!IsInstanceValid(body))
                continue;

            body.LinearVelocity = push;
            GD.Print($"[ConveyorBelt:{Name}] Pushing {body.Name}");
        }
    }

    private void OnBodyEntered(RigidBody3D body)
    {
        if (body == null || _bodiesOnBelt.Contains(body)) 
            return;
        GD.Print($"[ConveyorBelt:{Name}] Adding {body.Name}");
        _bodiesOnBelt.Add(body);
    }

    private void OnBodyExited(RigidBody3D body)
    {
        if (body == null) 
            return;
        GD.Print($"[ConveyorBelt:{Name}] Removing {body.Name}");
        _bodiesOnBelt.Remove(body);
    }
}
