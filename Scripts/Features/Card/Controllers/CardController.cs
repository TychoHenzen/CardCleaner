using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Controllers;

[Tool]
public partial class CardController : RigidBody3D
{
    private readonly List<ICardComponent> _components = new();
    private readonly List<IPhysicsComponent> _physicsComponents = new();
    public Models.CardSignature Signature;

    public override void _Ready()
    {
        DiscoverComponents(this);
        AddToGroup("Cards");
        CollisionLayer = 2;
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