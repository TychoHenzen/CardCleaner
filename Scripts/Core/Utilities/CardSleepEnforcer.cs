using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Utilities;

/// <summary>
///     Forces the card to re-enter sleep if motion is below threshold,
///     to prevent wakeups from other cards landing on it.
/// </summary>
public partial class CardSleepEnforcer : Node, IPhysicsComponent
{
    // Default values as constants
    private const float DefaultAngularSleepThreshold = 0.05f;
    private const float DefaultLinearSleepThreshold = 0.05f;

    private RigidBody3D _body = null!;
    [Export] public float AngularSleepThreshold = DefaultAngularSleepThreshold;
    [Export] public float LinearSleepThreshold = DefaultLinearSleepThreshold;

    public void Setup(Node cardRoot) => _body = (cardRoot as RigidBody3D)!;

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

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(AngularSleepThreshold) => true,
            nameof(LinearSleepThreshold) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(AngularSleepThreshold) => DefaultAngularSleepThreshold,
            nameof(LinearSleepThreshold) => DefaultLinearSleepThreshold,
            _ => base._PropertyGetRevert(property)
        };
    }
}
