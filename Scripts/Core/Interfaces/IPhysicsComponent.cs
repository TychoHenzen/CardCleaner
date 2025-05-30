using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IPhysicsComponent : ICardComponent
{
    void IntegrateForces(PhysicsDirectBodyState3D state);
    void PhysicsProcess(double delta);
}