using System.Collections.Generic;
using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Controllers;

[Tool]
public partial class CardController : RigidBody3D {
    private readonly List<ICardComponent> _components = new();

    public override void _Ready() {
        // Discover and initialize all child components
        foreach (var child in GetChildren())
        {
            if (child is not ICardComponent comp) continue;
            
            comp.Setup(this);
            _components.Add(comp);
        }
        AddToGroup("Cards");
        
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state) {
        // Delegate physics updates
        foreach (var comp in _components) {
            comp.IntegrateForces(state);
        }
    }
}