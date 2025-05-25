using System;
using System.Collections.Generic;
using System.Linq;
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

    private Camera3D _camera;
    private RigidBody3D _lastCard;

    // Pickup/drop state
    private readonly List<RigidBody3D> _heldCards = new();
    private Node3D _handAnchor;
    private Node3D _cardsParent;
    [Export] public float HoldDistance = 2f;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>(CameraPath);
        SetupCardCollisionLayers();

        // Use camera as hand anchor, cache original Cards node
        _handAnchor = _camera;
        _cardsParent = GetParent().GetNode<Node3D>("Cards");
    }

    public override void _Input(InputEvent @event)
    {
        if (Engine.IsEditorHint()) return;
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left && _lastCard != null)
                PickUpCard(_lastCard);
            if (mb.ButtonIndex == MouseButton.Right)
                DropAllHeldCards();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        RigidBody3D target = FindCard();
        if (target != null && _camera.GlobalPosition.DistanceTo(target.GlobalPosition) <= MaxHighlightDistance)
        {
            if (target == _lastCard) return;
            ClearOutline();
            ApplyOutline(target);
        }
        else
        {
            ClearOutline();
        }
    }

    private void PickUpCard(RigidBody3D card)
    {
        if (_heldCards.Contains(card)) return;

        // Disable physics
        card.Freeze = true;
        var shape = card.GetNode<CollisionShape3D>("CardCollision");
        if (shape != null) shape.Disabled = true;
        var designer = card.GetNode<CardCleaner.Scripts.CardDesigner>("Designer");

        // Reparent to camera and stack
        card.GetParent().RemoveChild(card);
        _handAnchor.AddChild(card);
        float thickness = designer.Thickness;
        float yOffset = _heldCards.Count * thickness;
        card.Transform = new Transform3D(Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(90)), 
            new Vector3(1-yOffset*10, 0, -HoldDistance+yOffset));

        _heldCards.Add(card);
    }

    private void DropAllHeldCards()
    {
        if (_heldCards.Count == 0) return;

        for (var index = 0; index < _heldCards.Count; index++)
        {
            var card = _heldCards[index];
            // Re-enable physics
            card.Freeze = false;
            var shape = card.GetNode<CollisionShape3D>("CardCollision");
            if (shape != null) shape.Disabled = false;
            card.CollisionLayer = CardCollisionLayer;

            // Reparent back and drop
            card.GetParent().RemoveChild(card);
            _cardsParent.AddChild(card);
            float thickness = 0.005f;
            float yOffset = index * (thickness + 0.002f);
            Vector3 forward = -_camera.GlobalTransform.Basis.Z;
            Vector3 dropPos = _camera.GlobalTransform.Origin + forward * HoldDistance + _camera.GlobalTransform.Basis.Y * yOffset;
            card.GlobalTransform = new Transform3D(Basis.Identity, dropPos);
        }

        _heldCards.Clear();
    }

    // --- Ray‐casting methods unchanged ---
    private RigidBody3D FindCard()
    {
        var origin = _camera.GlobalTransform.Origin;
        var forward = -_camera.GlobalTransform.Basis.Z;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D {
            From = origin, To = origin + forward * RayLength,
            CollideWithBodies = true, CollideWithAreas = false,
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
        if (cards.Count == 0) cards = FindCardsByName();

        foreach (Node node in cards)
            if (node is RigidBody3D rb)
                rb.CollisionLayer = CardCollisionLayer;
    }

    private Godot.Collections.Array<Node> FindCardsByName()
    {
        var list = new Godot.Collections.Array<Node>();
        Search(GetTree().Root);
        return list;

        void Search(Node p) {
            if (p is RigidBody3D r && r.Name.ToString().StartsWith("Card")) list.Add(r);
            foreach (Node c in p.GetChildren()) Search(c);
        }
    }
}
