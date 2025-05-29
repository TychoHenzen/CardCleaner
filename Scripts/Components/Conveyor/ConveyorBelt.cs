// ConveyorBeltTweenToMarker.cs
using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class ConveyorBelt : Node3D
{
    private const string TweenMetaKey = "active_conveyor_tween";
    [Export] public Area3D DetectionArea { get; set; }
    [Export] public Node3D DestinationMarker { get; set; }

    [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
    public float Speed { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "0.0, 5.0, 0.1")]
    public float LateralOffsetRange { get; set; } = 1.0f;

    private BoxShape3D _areaBox;
    private readonly List<RigidBody3D> _onBelt = new();

    public override void _Ready()
    {
        if (DetectionArea == null || DestinationMarker == null)
        {
            GD.PushError("[ConveyorBelt] Assign DetectionArea and DestinationMarker.");
            return;
        }

        var collision = DetectionArea.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        _areaBox = collision?.Shape as BoxShape3D;
        if (_areaBox == null)
        {
            GD.PushError("[ConveyorBelt] DetectionArea needs a BoxShape3D child.");
            return;
        }

        DetectionArea.BodyEntered += OnBodyEntered;
        DetectionArea.BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not RigidBody3D card)
            return;
        if (_onBelt.Contains(card))
            return;

        _onBelt.Add(card);
        // Cancel any falling motion immediately
        var v = card.LinearVelocity;
        card.LinearVelocity = v;
        // Make sure no unexpected damping slows it
        // card.LinearDamp = 0;
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is RigidBody3D card)
            _onBelt.Remove(card);
    }

    public override void _PhysicsProcess(double delta)
    {
        for (int i = _onBelt.Count - 1; i >= 0; i--)
        {
            var card = _onBelt[i];
            if (!IsInstanceValid(card))
            {
                _onBelt.RemoveAt(i);
                continue;
            }
            if(card.Sleeping)
                card.SetSleeping(false);

            // Destination + offset, ignoring vertical component
            Vector3 target = DestinationMarker.GlobalPosition;
            Vector3 dir = target - card.GlobalPosition;
            dir.Y += 0.1f;

            // Drive the card straight toward the marker
            card.LinearVelocity = dir.Normalized() * Speed;
        }
    }
}