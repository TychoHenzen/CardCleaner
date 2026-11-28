using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Card.Controllers;

public partial class CardController : RigidBody3D, IInteractable, ISaveable
{
    [Signal]
    public delegate void CardPickedUpEventHandler(CardController card);

    private readonly List<ICardComponent> _components = new();
    private readonly List<IPhysicsComponent> _physicsComponents = new();
    private CardSignature _signature;
    public bool HasSignature { get; private set; }

    public CardSignature Signature
    {
        get => _signature;
        set
        {
            _signature = value;
            HasSignature = true;
        }
    }

    internal void ClearSignature()
    {
        _signature = default;
        HasSignature = false;
    }

    /// <summary>
    /// The look seed of a card with no magical potential, which drives its art and base type. Null for special
    /// cards and for saves written before look seeds existed.
    /// </summary>
    public int? LookSeed { get; set; }

    /// <summary>
    /// The price the player set for this card in the shop, or null while it asks its market price. It travels with the
    /// card from slot to slot and is saved with it.
    /// </summary>
    public int? ShopPrice { get; set; }

    private bool IsHeld => GetParent() is Camera3D; // Check if reparented to camera

    // IInteractable implementation. The hand router reads this and the hand's room before it plans a pick-up, so a
    // card the hand would not take offers no click.
    public bool CanInteract => !IsHeld;
    public Node3D InteractionBody => this;

    // Arm's reach, as for boxes and packs (CardContainer); a card across the room is not picked up.
    public float InteractionRange => 4.5f;

    public string Prompt => "Pick up card";

    // The hand router plans every click on a card (it claims this target).
    public void Interact()
    {
    }

    public void Highlight()
    {
        var outline = GetNodeOrNull<Node3D>("OutlineBox");
        if (outline != null) outline.Visible = true;
    }

    public void ClearHighlight()
    {
        var outline = GetNodeOrNull<Node3D>("OutlineBox");
        if (outline != null) outline.Visible = false;
    }

    // ISaveable implementation
    public StringName UniqueID => $"card_{GetInstanceId()}";

    public void Save(NodeSave save)
    {
        save.SetOrAddProperty("signature", HasSignature ? Signature : (CardSignature?)null);
        save.SetOrAddProperty("lookSeed", LookSeed);
        save.SetOrAddProperty("position", GlobalPosition);
        save.SetOrAddProperty("rotation", GlobalRotation);
    }

    public void Load(NodeSave save)
    {
        if (save.TryGetProperty("signature", out CardSignature? signature) && signature.HasValue)
            Signature = signature.Value;
        else
            ClearSignature();

        LookSeed = save.TryGetProperty("lookSeed", out int? seed) ? seed : null;

        if (save.TryGetProperty<Vector3>("position", out var pos))
            GlobalPosition = pos;

        if (save.TryGetProperty<Vector3>("rotation", out var rot))
            GlobalRotation = rot;
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
            // The face sets up its own parts (each finds its siblings through the face); the card only drives its
            // blacklight each physics frame.
            if (child is CardFace face)
            {
                _physicsComponents.AddRange(face.PhysicsComponents);
                continue;
            }

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
