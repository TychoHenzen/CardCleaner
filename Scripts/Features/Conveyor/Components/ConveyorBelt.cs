using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
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
        var area = DetectionArea;
        // ExportCheck is true for a missing export. Both are checked so each missing one is reported.
        var areaMissing = NodeExports.ExportCheck(area, nameof(DetectionArea), this);
        var markerMissing = NodeExports.ExportCheck(DestinationMarker, nameof(DestinationMarker), this);
        if (areaMissing || markerMissing || area == null || !TryCacheAreaBox(area))
            return;

        area.BodyEntered += OnBodyEntered;
        area.BodyExited += OnBodyExited;
    }

    public override void _PhysicsProcess(double delta)
    {
        var marker = DestinationMarker;
        if (marker == null)
            return;

        _onBelt.RemoveAll(card => !IsInstanceValid(card));
        foreach (var card in _onBelt)
            DriveCard(card, marker);
    }

    private bool TryCacheAreaBox(Area3D area)
    {
        var collision = area.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        _areaBox = collision?.Shape as BoxShape3D;
        if (_areaBox != null)
            return true;

        ILog.Error("DetectionArea needs a BoxShape3D child.");
        return false;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is RigidBody3D card && !_onBelt.Contains(card))
            _onBelt.Add(card);
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is RigidBody3D card)
            _onBelt.Remove(card);
    }

    private void DriveCard(RigidBody3D card, Node3D marker)
    {
        if (card.Sleeping)
            card.SetSleeping(false);

        // Destination + offset, ignoring vertical component
        var dir = marker.GlobalPosition - card.GlobalPosition;
        dir.Y += 0.1f;

        // Drive the card straight toward the marker
        card.LinearVelocity = dir.Normalized() * Speed;
    }
}
