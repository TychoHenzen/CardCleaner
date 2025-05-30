using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

[Tool]
public partial class FlutterCard : Node, IPhysicsComponent
{
    private float _flipPhase;

    // RNG and phase offsets for two flutter modes
    private RandomNumberGenerator _rng;
    private float _twistPhase;

    [Export] public float AirDensity = 1.0f;

    // Aerodynamic coefficients
    [Export] public float DragCoeff = 0.5f;
    [Export] public float FlutterPitch = 0.05f; // flip around side
    [Export] public float FlutterTwist = 0.1f; // twist around normal
    [Export] public float LiftCoeff = 0.2f;

    public void Setup(RigidBody3D cardRoot)
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = (uint)GetInstanceId();

        // Different starting points for each flutter mode
        _twistPhase = _rng.RandfRange(0, Mathf.Pi * 2);
        _flipPhase = _rng.RandfRange(0, Mathf.Pi * 2);
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        var v = state.LinearVelocity;
        var speed = v.Length();
        if (speed < 0.01f)
            return;

        // Noise for drag & lift (±5%), and separate noise for each flutter (±20%)
        var dragNoise = _rng.RandfRange(-0.05f, 0.05f);
        var liftNoise = _rng.RandfRange(-0.05f, 0.05f);
        var twistNoise = _rng.RandfRange(-0.2f, 0.2f);
        var pitchNoise = _rng.RandfRange(-0.2f, 0.2f);

        // 1) Drag
        var drag = -v.Normalized()
                   * (0.5f * AirDensity * DragCoeff * speed * speed)
                   * (1 + dragNoise);
        state.ApplyForce(drag, Vector3.Zero);

        // 2) Lift
        var normal = -state.Transform.Basis.X; // card face normal
        var side = normal.Cross(v).Normalized(); // wing axis
        var liftDir = side.Cross(normal).Normalized();
        var lift = liftDir
                   * (0.5f * AirDensity * LiftCoeff * speed * speed)
                   * (1 + liftNoise);
        state.ApplyForce(lift, Vector3.Zero);

        // Time in seconds
        var t = Time.GetTicksMsec() / 1000f;

        // 3a) Twist flutter around normal
        var twistAngle = Mathf.Sin((t + _twistPhase) * 5f);
        var twistTorque = normal
                          * twistAngle
                          * FlutterTwist
                          * speed
                          * (1 + twistNoise);

        // 3b) Pitch/flutter around side axis (width-wise)
        var flipAngle = Mathf.Sin((t + _flipPhase) * 4f); // slightly different frequency
        var pitchTorque = side
                          * flipAngle
                          * FlutterPitch
                          * speed
                          * (1 + pitchNoise);

        // Apply both
        state.ApplyTorque(twistTorque + pitchTorque);
    }

    public void PhysicsProcess(double delta)
    {
        //no-op
    }
}