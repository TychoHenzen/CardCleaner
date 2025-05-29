using CardCleaner.Scripts.Interfaces;
using Godot;

[Tool]
public partial class FlutterCard : Node, ICardComponent
{
    // Aerodynamic coefficients
    [Export] public float DragCoeff = 0.5f;
    [Export] public float LiftCoeff = 0.2f;
    [Export] public float FlutterTwist = 0.1f; // twist around normal
    [Export] public float FlutterPitch = 0.05f; // flip around side
    [Export] public float AirDensity = 1.0f;

    // RNG and phase offsets for two flutter modes
    private RandomNumberGenerator _rng;
    private float _twistPhase;
    private float _flipPhase;

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
        float speed = v.Length();
        if (speed < 0.01f)
            return;

        // Noise for drag & lift (±5%), and separate noise for each flutter (±20%)
        float dragNoise = _rng.RandfRange(-0.05f, 0.05f);
        float liftNoise = _rng.RandfRange(-0.05f, 0.05f);
        float twistNoise = _rng.RandfRange(-0.2f, 0.2f);
        float pitchNoise = _rng.RandfRange(-0.2f, 0.2f);

        // 1) Drag
        Vector3 drag = -v.Normalized()
                       * (0.5f * AirDensity * DragCoeff * speed * speed)
                       * (1 + dragNoise);
        state.ApplyForce(drag, Vector3.Zero);

        // 2) Lift
        Vector3 normal = -state.Transform.Basis.X; // card face normal
        Vector3 side = normal.Cross(v).Normalized(); // wing axis
        Vector3 liftDir = side.Cross(normal).Normalized();
        Vector3 lift = liftDir
                       * (0.5f * AirDensity * LiftCoeff * speed * speed)
                       * (1 + liftNoise);
        state.ApplyForce(lift, Vector3.Zero);

        // Time in seconds
        float t = Time.GetTicksMsec() / 1000f;

        // 3a) Twist flutter around normal
        float twistAngle = Mathf.Sin((t + _twistPhase) * 5f);
        Vector3 twistTorque = normal
                              * twistAngle
                              * FlutterTwist
                              * speed
                              * (1 + twistNoise);

        // 3b) Pitch/flutter around side axis (width-wise)
        float flipAngle = Mathf.Sin((t + _flipPhase) * 4f); // slightly different frequency
        Vector3 pitchTorque = side
                              * flipAngle
                              * FlutterPitch
                              * speed
                              * (1 + pitchNoise);

        // Apply both
        state.ApplyTorque(twistTorque + pitchTorque);
    }
}