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
    
    // Collision layer settings
    [Export] public uint CardCollisionLayer = 2; // Layer 2 for cards
    [Export] public bool IgnorePlayer = true;
    [Export] public bool IgnoreFloor = true;

    private Camera3D _camera;
    private RigidBody3D _lastCard;
    private Material _standardMat;
    private ShaderMaterial _outlineMat = GD.Load<ShaderMaterial>("res://Shaders/OutlineMaterial.tres");

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>(CameraPath);
        _standardMat = GD.Load<Material>("res://Materials/StandardMaterial.tres");
        
        // Set all cards to the specified collision layer
        SetupCardCollisionLayers();
    }

    private void SetupCardCollisionLayers()
    {
        // Find all cards and put them on the specified layer
        var cards = GetTree().GetNodesInGroup("Cards");
        if (cards.Count == 0)
        {
            // If no group, find by name pattern
            cards = FindCardsByName();
        }

        foreach (Node card in cards)
        {
            if (card is RigidBody3D rb)
            {
                rb.CollisionLayer = CardCollisionLayer;
                // Optionally also set collision mask if cards should only interact with specific things
                if (EnableDebug)
                    GD.Print($"Set {rb.Name} to collision layer {CardCollisionLayer}");
            }
        }
    }

    private Godot.Collections.Array<Node> FindCardsByName()
    {
        var cards = new Godot.Collections.Array<Node>();
        var allNodes = GetTree().GetNodesInGroup("Cards");
        
        if (allNodes.Count == 0)
        {
            // Fallback: search by name pattern
            SearchForCards(GetTree().Root, cards);
        }
        
        return cards;
    }

    private void SearchForCards(Node parent, Godot.Collections.Array<Node> cards)
    {
        if (parent is RigidBody3D rb && rb.Name.ToString().StartsWith("Card"))
        {
            cards.Add(rb);
        }

        foreach (Node child in parent.GetChildren())
        {
            SearchForCards(child, cards);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        RigidBody3D targetCard = null;

        if (UseMultipleRays)
        {
            targetCard = FindCardWithMultipleRays();
        }
        else
        {
            targetCard = FindCardWithSingleRay();
        }

        // Distance check
        if (targetCard != null)
        {
            float distance = _camera.GlobalPosition.DistanceTo(targetCard.GlobalPosition);
            if (distance > MaxHighlightDistance)
            {
                targetCard = null;
            }
        }

        // Apply or clear outline
        if (targetCard != null)
        {
            if (targetCard != _lastCard)
            {
                ClearOutline();
                ApplyOutline(targetCard);
            }
        }
        else
        {
            ClearOutline();
        }
    }

    private RigidBody3D FindCardWithSingleRay()
    {
        var origin = _camera.GlobalTransform.Origin;
        var forward = -_camera.GlobalTransform.Basis.Z;
        var target = origin + forward * RayLength;

        var rayQuery = new PhysicsRayQueryParameters3D
        {
            From = origin,
            To = target,
            CollideWithBodies = true,
            CollideWithAreas = false,
            CollisionMask = CardCollisionLayer // Only hit the card layer
        };

        var result = GetWorld3D().DirectSpaceState.IntersectRay(rayQuery);
        
        if (result.Count > 0)
        {
            var collider = result["collider"].Obj;
            if (collider is RigidBody3D rb && rb.Name.ToString().StartsWith("Card"))
            {
                return rb;
            }
        }
        
        return null;
    }

    private RigidBody3D FindCardWithMultipleRays()
    {
        var origin = _camera.GlobalTransform.Origin;
        var forward = -_camera.GlobalTransform.Basis.Z;
        var right = _camera.GlobalTransform.Basis.X;
        var up = _camera.GlobalTransform.Basis.Y;

        var rayDirections = new List<Vector3>
        {
            forward,                           // Center
            forward + right * CrosshairSize,   // Right
            forward - right * CrosshairSize,   // Left  
            forward + up * CrosshairSize,      // Up
            forward - up * CrosshairSize,      // Down
        };

        var cardHits = new Dictionary<RigidBody3D, (int hits, float distance)>();

        foreach (var direction in rayDirections)
        {
            var normalizedDir = direction.Normalized();
            var target = origin + normalizedDir * RayLength;

            var rayQuery = new PhysicsRayQueryParameters3D
            {
                From = origin,
                To = target,
                CollideWithBodies = true,
                CollideWithAreas = false,
                CollisionMask = CardCollisionLayer // Only hit the card layer
            };

            var result = GetWorld3D().DirectSpaceState.IntersectRay(rayQuery);
            
            if (result.Count > 0)
            {
                var collider = result["collider"].Obj;
                if (collider is RigidBody3D rb && rb.Name.ToString().StartsWith("Card"))
                {
                    float distance = origin.DistanceTo(rb.GlobalPosition);
                    
                    if (cardHits.ContainsKey(rb))
                    {
                        var existing = cardHits[rb];
                        cardHits[rb] = (existing.hits + 1, Math.Min(existing.distance, distance));
                    }
                    else
                    {
                        cardHits[rb] = (1, distance);
                    }
                }
            }
        }

        if (cardHits.Count == 0) return null;

        return cardHits
            .OrderByDescending(kvp => kvp.Value.hits)
            .ThenBy(kvp => kvp.Value.distance)
            .First().Key;
    }

    private void ApplyOutline(RigidBody3D card)
    {
        try
        {
            var mesh = card.GetNode<CsgBox3D>("CSGBox3D");
            mesh.Material = _outlineMat;
            _lastCard = card;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to apply outline to {card.Name}: {e.Message}");
        }
    }

    private void ClearOutline()
    {
        if (_lastCard == null) return;

        try
        {
            var mesh = _lastCard.GetNode<CsgBox3D>("CSGBox3D");
            mesh.Material = _standardMat;
            _lastCard = null;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to clear outline: {e.Message}");
            _lastCard = null;
        }
    }
}