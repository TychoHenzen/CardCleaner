using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Card.Controllers;

public partial class CardController : RigidBody3D, IInteractable, ISaveable
{
    [Signal]
    public delegate void CardInteractionRequestedEventHandler(CardController card);

    [Signal]
    public delegate void CardPickedUpEventHandler(CardController card);

    private readonly List<ICardComponent> _components = new();
    private readonly List<IPhysicsComponent> _physicsComponents = new();
    public CardSignature Signature = null!;

    private bool IsHeld => GetParent() is Camera3D; // Check if reparented to camera

    // IInteractable implementation
    public bool CanInteract => !IsHeld;
    public Node3D InteractionBody => this;
    public float InteractionRange => 50f;

    public void Interact()
    {
        if (CanInteract) EmitSignal(SignalName.CardInteractionRequested, this);
    }

    public void Highlight()
    {
        var outline = GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (outline != null) outline.Visible = true;
    }

    public void ClearHighlight()
    {
        var outline = GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (outline != null) outline.Visible = false;
    }

    // ISaveable implementation
    public StringName UniqueID => $"card_{GetInstanceId()}";

    public void Save(NodeSave save)
    {
        save.SetOrAddProperty("signature", Signature);
        save.SetOrAddProperty("position", GlobalPosition);
        save.SetOrAddProperty("rotation", GlobalRotation);
        save.SetOrAddProperty("isHeld", IsHeld);
    }

    public void Load(NodeSave save)
    {
        if (save.TryGetProperty("signature", out CardSignature? signature) && signature != null)
            Signature = signature;

        if (save.TryGetProperty<Vector3>("position", out var pos))
            GlobalPosition = pos;

        if (save.TryGetProperty<Vector3>("rotation", out var rot))
            GlobalRotation = rot;

        // Note: IsHeld state will be restored by parent relationship during scene rebuild
    }

    public override void _Ready()
    {
        DiscoverComponents(this);
        AddToGroup("Cards");
        CollisionLayer = 2;
    }

    public void EmitPickupSignal()
    {
        EmitSignal(SignalName.CardPickedUp, this);
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
