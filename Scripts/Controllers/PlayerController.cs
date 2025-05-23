using Godot;
using System;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float Speed = 5.0f;
    [Export] public float JumpVelocity = 10.0f;
    [Export] public float MouseSensitivity = 0.1f;
    [Export] public float MinPitch = -80f;
    [Export] public float MaxPitch =  80f;

    private Node3D _head;
    private float _pitchDeg = 0f;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            float yawDelta = -motion.Relative.X * MouseSensitivity;
            RotateY(Mathf.DegToRad(yawDelta));

            _pitchDeg = Mathf.Clamp(_pitchDeg - motion.Relative.Y * MouseSensitivity, MinPitch, MaxPitch);
            _head.RotationDegrees = new Vector3(_pitchDeg, 0, 0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 input = new Vector2(
            Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left"),
            Input.GetActionStrength("ui_down")  - Input.GetActionStrength("ui_up")
        ).Normalized();

        var vel = Velocity;
        Vector3 dir = (Transform.Basis.X * input.X) + (Transform.Basis.Z * input.Y);
        vel.X = dir.X * Speed;
        vel.Z = dir.Z * Speed;

        if (IsOnFloor() && Input.IsActionJustPressed("ui_accept"))
            vel.Y = JumpVelocity;

        float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
        vel.Y -= gravity * (float)delta;

        Velocity = vel;
        MoveAndSlide();
    }
}