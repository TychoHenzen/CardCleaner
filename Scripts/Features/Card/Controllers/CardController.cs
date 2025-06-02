using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Controllers;

[Tool]
public partial class CardController : RigidBody3D, IInteractable
{
    [Signal]
    public delegate void CardPickedUpEventHandler(CardController card);
    
    [Signal] 
    public delegate void CardInteractionRequestedEventHandler(CardController card);
    
    private readonly List<ICardComponent> _components = new();
    private readonly List<IPhysicsComponent> _physicsComponents = new();
    public Models.CardSignature Signature;

    // IInteractable implementation
    public bool CanInteract => !IsHeld;
    public Node3D InteractionBody => this;
    public float InteractionRange => 50f;
    
    private bool IsHeld => GetParent() is Camera3D; // Check if reparented to camera

    public void Interact()
    {
        if (CanInteract)
        {
            EmitSignal(nameof(CardInteractionRequested), this);
        }
    }

    public void Highlight()
    {
        var outline = GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (outline != null) 
        {
            outline.Visible = true;
        }
    }

    public void ClearHighlight()
    {
        var outline = GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (outline != null) 
        {
            outline.Visible = false;
        }
    }

    public override void _Ready()
    {
        DiscoverComponents(this);
        AddToGroup("Cards");
        CollisionLayer = 2;
    }
    
    public void EmitPickupSignal()
    {
        EmitSignal(nameof(CardPickedUp), this);
    }

    private void DiscoverComponents(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is ICardComponent comp)
            {
                comp.Setup(this);
                _components.Add(comp);

                switch (child)
                {
                    case IPhysicsComponent physicsComp:
                        _physicsComponents.Add(physicsComp);
                        break;
                }
            }

            // Recurse into children
            DiscoverComponents(child);
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        base._IntegrateForces(state);
        // Only call physics on components that actually need it
        foreach (var physicsComp in _physicsComponents) physicsComp.IntegrateForces(state);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        // Only call physics on components that actually need it
        foreach (var physicsComp in _physicsComponents) physicsComp.PhysicsProcess(delta);
    }
}