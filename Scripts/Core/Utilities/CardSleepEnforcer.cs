using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Utilities;

/// <summary>
///     Forces the card to re-enter sleep if motion is below threshold,
///     to prevent wakeups from other cards landing on it.
/// </summary>
[Tool]
public partial class CardSleepEnforcer : Node, IPhysicsComponent
{
    private RigidBody3D _body;
    [Export] public float AngularSleepThreshold = 0.05f;
    [Export] public float LinearSleepThreshold = 0.05f;

    public void Setup(RigidBody3D cardRoot)
    {
        _body = cardRoot;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (state.LinearVelocity.LengthSquared() < LinearSleepThreshold * LinearSleepThreshold &&
            state.AngularVelocity.LengthSquared() < AngularSleepThreshold * AngularSleepThreshold)
            _body.Freeze = true;
    }

    public void PhysicsProcess(double delta)
    {
        //no-op
    }
}