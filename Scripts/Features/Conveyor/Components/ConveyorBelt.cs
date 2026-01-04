// ConveyorBeltTweenToMarker.cs

using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Conveyor.Components;

public partial class ConveyorBelt : Node3D
{
    private readonly List<RigidBody3D> _onBelt = new();

    private BoxShape3D? _areaBox;
    [Export] public Area3D? DetectionArea { get; set; }
    [Export] public Node3D? DestinationMarker { get; set; }

    [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
    public float Speed { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "0.0, 5.0, 0.1")]
    public float LateralOffsetRange { get; set; } = 1.0f;

    public override void _Ready()
    {
        if (ILog.ExportCheck(DetectionArea, nameof(DetectionArea), this) ||
            ILog.ExportCheck(DestinationMarker, nameof(DestinationMarker), this))
            return;

        var collision = DetectionArea.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        _areaBox = collision?.Shape as BoxShape3D;
        if (_areaBox == null)
        {
            ILog.Error("DetectionArea needs a BoxShape3D child.");
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
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is RigidBody3D card)
            _onBelt.Remove(card);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (DestinationMarker == null)
            return;

        var writeIndex = 0;
        for (var i = 0; i < _onBelt.Count; i++)
        {
            var card = _onBelt[i];
            if (!IsInstanceValid(card))
                continue;

            // Compact valid cards to front of list
            if (writeIndex != i)
                _onBelt[writeIndex] = card;

            writeIndex++;

            if (card.Sleeping)
                card.SetSleeping(false);

            // Destination + offset, ignoring vertical component
            var target = DestinationMarker.GlobalPosition;
            var dir = target - card.GlobalPosition;
            dir.Y += 0.1f;

            // Drive the card straight toward the marker
            card.LinearVelocity = dir.Normalized() * Speed;
        }

        // Remove all invalid cards at once (single O(k) operation)
        if (writeIndex < _onBelt.Count)
            _onBelt.RemoveRange(writeIndex, _onBelt.Count - writeIndex);
    }
}
