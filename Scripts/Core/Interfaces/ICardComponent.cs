using Godot;

namespace CardCleaner.Scripts.Interfaces;

public interface ICardComponent {
    void Setup(RigidBody3D cardRoot);
    void IntegrateForces(PhysicsDirectBodyState3D state);
}