using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     Freezes the card once it has stayed below the motion thresholds for <see cref="RestSeconds" />, or once Jolt
///     puts it to sleep, so a card at rest is not woken by other cards landing on it. A landing card is stopped by the
///     floor a few frames before Jolt has pushed it back out of the floor, so freezing on the first slow frame left
///     cards sunk out of sight.
/// </summary>
public partial class CardSleepEnforcer : Node, IPhysicsComponent
{
    // Default values as constants
    private const float DefaultAngularSleepThreshold = 0.05f;
    private const float DefaultLinearSleepThreshold = 0.05f;

    // ASSUMPTION: half a second, the same rest time Jolt waits before it lets a body sleep
    // (physics/jolt_physics_3d/simulation/sleep_time_threshold), is enough for the contact solver to resolve a
    // landing's penetration; CardFloorRestTest checks it on the real card.
    private const float DefaultRestSeconds = 0.5f;

    private RigidBody3D _body = null!;
    private float _restedSeconds;
    [Export] public float AngularSleepThreshold = DefaultAngularSleepThreshold;
    [Export] public float LinearSleepThreshold = DefaultLinearSleepThreshold;

    /// <summary>How long the card must stay below both thresholds before it is frozen, in seconds.</summary>
    [Export] public float RestSeconds = DefaultRestSeconds;

    public void Setup(Node cardRoot)
    {
        _body = (cardRoot as RigidBody3D)!;
        if (_body != null)
            _body.SleepingStateChanged += OnSleepingStateChanged;
    }

    // Jolt sleeps a body that has rested for its own sleep time, as long as RestSeconds, and a sleeping body gets no
    // more IntegrateForces calls, so when Jolt's count ends first the one above never does. A card asleep has rested.
    private void OnSleepingStateChanged()
    {
        if (_body.Sleeping)
            _body.Freeze = true;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (Rest(state.LinearVelocity, state.AngularVelocity, state.Step))
            _body.Freeze = true;
    }

    /// <summary>
    ///     Counts one physics step of <paramref name="step" /> seconds at the given velocities, and says whether the
    ///     card has now rested long enough to freeze. Any step above a threshold starts the count again, and so does
    ///     the freeze itself, so a card that is thrown again rests anew.
    /// </summary>
    internal bool Rest(Vector3 linearVelocity, Vector3 angularVelocity, float step)
    {
        var slow = linearVelocity.LengthSquared() < LinearSleepThreshold * LinearSleepThreshold
                   && angularVelocity.LengthSquared() < AngularSleepThreshold * AngularSleepThreshold;
        _restedSeconds = slow ? _restedSeconds + step : 0f;
        if (_restedSeconds < RestSeconds)
            return false;

        _restedSeconds = 0f;
        return true;
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
            nameof(RestSeconds) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(AngularSleepThreshold) => DefaultAngularSleepThreshold,
            nameof(LinearSleepThreshold) => DefaultLinearSleepThreshold,
            nameof(RestSeconds) => DefaultRestSeconds,
            _ => base._PropertyGetRevert(property)
        };
    }
}
