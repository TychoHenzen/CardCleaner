using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts;

/// <summary>
/// Forces the card to re-enter sleep if motion is below threshold,
/// to prevent wakeups from other cards landing on it.
/// </summary>
[Tool]
public partial class CardSleepEnforcer : Node, IPhysicsComponent
{
    [Export] public float LinearSleepThreshold = 0.05f;
    [Export] public float AngularSleepThreshold = 0.05f;

    private RigidBody3D _body;

    public void Setup(RigidBody3D cardRoot)
    {
        _body = cardRoot;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (state.LinearVelocity.LengthSquared() < LinearSleepThreshold * LinearSleepThreshold &&
            state.AngularVelocity.LengthSquared() < AngularSleepThreshold * AngularSleepThreshold)
        {
            _body.Freeze = true;
        }
    }

    public void PhysicsProcess(double delta)
    {
        //no-op
    }
}