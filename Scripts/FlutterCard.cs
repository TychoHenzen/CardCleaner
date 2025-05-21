using Godot;
using System;

public partial class FlutterCard : RigidBody3D
{
    [Export] public float DragCoeff = 0.5f;        // descent speed
    [Export] public float LiftCoeff = 0.2f;        // sideways “bump”
    [Export] public float FlutterTorque = 0.1f;    // spin intensity
    [Export] public float AirDensity = 1.0f;       // airborne “weight”

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        var v = state.LinearVelocity;
        float speed = v.Length();
        if (speed < 0.01f) return;

        // 1) Aerodynamic drag: slows straight fall
        Vector3 drag = -v.Normalized() * (0.5f * AirDensity * DragCoeff * speed * speed);
        state.ApplyForce(drag, Vector3.Zero);

        // 2) Lift: pushes card up/down and sideways
        Vector3 normal = state.Transform.Basis.Y;          // card’s “face” normal
        Vector3 side   = normal.Cross(v).Normalized();     // wing axis
        Vector3 liftDir= side.Cross(normal).Normalized();  // lift direction
        Vector3 lift  = liftDir * (0.5f * AirDensity * LiftCoeff * speed * speed);
        state.ApplyForce(lift, Vector3.Zero);

        // 3) Flutter torque: oscillation about the normal
        float t = Time.GetTicksMsec() / 1000f;
        Vector3 torque = normal * Mathf.Sin(t * 5.0f) * FlutterTorque * speed;
        state.ApplyTorque(torque);
    }
}