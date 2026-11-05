// ConveyorBeltTweenToMarker.cs

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
        if (!ExportsAssigned(area) || area == null || !TryCacheAreaBox(area))
            return;

        area.BodyEntered += OnBodyEntered;
        area.BodyExited += OnBodyExited;
    }

    private bool ExportsAssigned(Area3D? area) =>
        !NodeExports.ExportCheck(area, nameof(DetectionArea), this) &&
        !NodeExports.ExportCheck(DestinationMarker,
            nameof(DestinationMarker), this);

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
        var marker = DestinationMarker;
        if (marker == null)
            return;

        RemoveInvalidCards();

        for (var i = 0; i < _onBelt.Count; i++)
            DriveCard(_onBelt[i], marker);
    }

    private void RemoveInvalidCards()
    {
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
        }

        // Remove all invalid cards at once (single O(k) operation)
        if (writeIndex < _onBelt.Count)
            _onBelt.RemoveRange(writeIndex, _onBelt.Count - writeIndex);
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
