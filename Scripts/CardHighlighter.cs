using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts;
using Godot;

public partial class CardHighlighter : Node3D
{
    [Export] public NodePath CameraPath;
    [Export] public float RayLength = 100f;
    [Export] public bool UseMultipleRays = true;
    [Export] public float CrosshairSize = 0.01f;
    [Export] public bool EnableDebug;
    [Export] public float MaxHighlightDistance = 50f;
    [Export] public uint CardCollisionLayer = 2;
    [Export] public float HoldDistance = 2f;

    private Camera3D _camera;
    private Node3D _handAnchor;
    private Node3D _cardsParent;
    private readonly List<RigidBody3D> _heldCards = new();
    private RigidBody3D _lastCard;

    // Drop-prep and preview
    private bool _isPreparingDrop = false;
    private MeshInstance3D _previewInstance;
    private ImmediateMesh _previewMesh;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>(CameraPath);
        _handAnchor = _camera;
        _cardsParent = GetParent().GetNode<Node3D>("Cards");
        SetupCardCollisionLayers();

        _previewMesh = new ImmediateMesh();
        _previewInstance = new MeshInstance3D
        {
            Mesh = _previewMesh,
            Visible = false
        };
        AddChild(_previewInstance);
    }

    public override void _Input(InputEvent @event)
    {
        if (Engine.IsEditorHint()) return;

        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Right)
            {
                if (mb.Pressed)
                    StartPrepareDrop();
                else
                    ReleasePrepareDrop();
            }
            else if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                if (_isPreparingDrop)
                    CancelPrepareDrop();
                else if (_lastCard != null)
                    PickUpCard(_lastCard);
            }
        }

        if (_isPreparingDrop && @event is InputEventKey keyEvt && keyEvt.Pressed && !keyEvt.Echo)
        {
            if (keyEvt.Keycode == Key.X)
                DropOneCard();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isPreparingDrop)
        {
            UpdateHeldCardPositions();
            UpdatePreview();
            return;
        }

        var target = FindCard();
        if (target != null && _camera.GlobalPosition.DistanceTo(target.GlobalPosition) <= MaxHighlightDistance)
        {
            if (target != _lastCard)
            {
                ClearOutline();
                ApplyOutline(target);
            }
        }
        else
        {
            ClearOutline();
        }
    }

    private void PickUpCard(RigidBody3D card)
    {
        if (_heldCards.Contains(card)) return;

        // Clear highlight and disable physics
        ClearOutline();
        card.Freeze = true;
        var shape = card.GetNode<CollisionShape3D>("CardCollision");
        if (shape != null) shape.Disabled = true;
        var designer = card.GetNode<CardCleaner.Scripts.CardDesigner>("Designer");

        // Reparent to hand anchor and stack flat
        card.GetParent().RemoveChild(card);
        _handAnchor.AddChild(card);
        float thickness = designer.Thickness;
        float indexOffset = _heldCards.Count * thickness;
        var rotation = Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(90));
        var localPos = new Vector3(
            1 - indexOffset * 10,
            0,
            -HoldDistance + indexOffset
        );
        card.Transform = new Transform3D(rotation, localPos);

        _heldCards.Add(card);
        _lastCard = null;
    }

    private void StartPrepareDrop()
    {
        if (_heldCards.Count == 0) return;
        _isPreparingDrop = true;
        _previewInstance.Visible = true;
    }

    private void CancelPrepareDrop()
    {
        _isPreparingDrop = false;
        _previewInstance.Visible = false;
    }

    private void ReleasePrepareDrop()
    {
        if (!_isPreparingDrop) return;
        DropAllHeldAtCurrent();
        _isPreparingDrop = false;
        _previewInstance.Visible = false;
    }

    private void DropOneCard()
    {
        if (_heldCards.Count == 0) return;
        int i = _heldCards.Count - 1;
        var card = _heldCards[i];
        EnablePhysics(card);
        // Remove from hand and reparent
        var temp = card.GlobalTransform;
        card.GetParent().RemoveChild(card);
        _cardsParent.AddChild(card);
        card.GlobalTransform = temp;
        _heldCards.RemoveAt(i);
    }

    private void DropAllHeldAtCurrent()
    {
        foreach (var card in _heldCards)
        {
            EnablePhysics(card);
            // Remove from hand and reparent
            var temp = card.GlobalTransform;
            card.GetParent().RemoveChild(card);
            _cardsParent.AddChild(card);
            card.GlobalTransform = temp;
        }
        _heldCards.Clear();
    }

    

    private void UpdateHeldCardPositions()
    {
        // Drop-prep: face camera yaw, hold horizontally, stacked up
        var camTransform = _camera.GlobalTransform;
        var forwardCam = -camTransform.Basis.Z;
        // Compute camera yaw only in XZ plane
        float yaw = Mathf.Atan2(forwardCam.X, forwardCam.Z);
        // Rotation: yaw around Y, then tilt flat
        var faceYaw = Basis.Identity.Rotated(Vector3.Up, yaw+Mathf.DegToRad(180));

        var downOffset = forwardCam * HoldDistance;
        var upAxis = Vector3.Up;

        for (int i = 0; i < _heldCards.Count; i++)
        {
            var card = _heldCards[i];
            float thickness = card.GetNode<CardDesigner>("Designer").Thickness;
            var worldPos = camTransform.Origin + downOffset + upAxis * (i * thickness);
            card.GlobalTransform = new Transform3D(faceYaw, worldPos);
        }
    }

    private void UpdatePreview()
    {
        if (_heldCards.Count == 0) return;

        var bottom = _heldCards[0];
        var origin = bottom.GlobalTransform.Origin;
        var to = origin + Vector3.Down * RayLength;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D {
            From = origin, To = to, CollideWithBodies = true
        });

        var hit = result.Count > 0 ? (Vector3)result["position"] : to;

        _previewMesh.ClearSurfaces();
        _previewMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        _previewMesh.SurfaceSetColor(Colors.Red);
        _previewMesh.SurfaceAddVertex(origin);
        _previewMesh.SurfaceAddVertex(hit);
        _previewMesh.SurfaceEnd();
    }

    private void EnablePhysics(RigidBody3D card)
    {
        card.Freeze = false;
        var shape = card.GetNode<CollisionShape3D>("CardCollision");
        if (shape != null) shape.Disabled = false;
        card.CollisionLayer = CardCollisionLayer;
    }

    private RigidBody3D FindCard()
    {
        var origin = _camera.GlobalTransform.Origin;
        var forward = -_camera.GlobalTransform.Basis.Z;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D {
            From = origin,
            To = origin + forward * RayLength,
            CollideWithBodies = true,
            CollideWithAreas = false,
            CollisionMask = CardCollisionLayer
        });
        if (result.Count > 0 && result["collider"].Obj is RigidBody3D rb && rb.Name.ToString().StartsWith("Card"))
            return rb;
        return null;
    }

    private void ApplyOutline(RigidBody3D card)
    {
        var outline = card.GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (outline == null) return;
        outline.Visible = true;
        _lastCard = card;
    }

    private void ClearOutline()
    {
        var outline = _lastCard?.GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (outline == null) return;
        outline.Visible = false;
        _lastCard = null;
    }

    private void SetupCardCollisionLayers()
    {
        var cards = GetTree().GetNodesInGroup("Cards");
        foreach (var card in cards.Cast<RigidBody3D>())
            card.CollisionLayer = CardCollisionLayer;
    }
}
