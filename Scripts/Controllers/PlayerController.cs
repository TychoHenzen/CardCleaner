using Godot;
using System;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float Speed = 5.0f;
    [Export] public float JumpVelocity = 10.0f;
    [Export] public float MouseSensitivity = 0.1f;
    [Export] public float MinPitch = -80f;
    [Export] public float MaxPitch =  80f;

    // Blacklight settings
    [Export] public bool BlacklightEnabled = false;
    [Export] public float BlacklightIntensity = 1.5f;

    private Node3D _head;
    private SpotLight3D _spotlight;
    private float _pitchDeg = 0f;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _spotlight = GetNode<SpotLight3D>("Head/Camera3D/SpotLight3D");
        
        // Configure spotlight as blacklight
        if (_spotlight != null)
        {
            _spotlight.SpotAngle = 60.0f;  // Wide cone
            _spotlight.SpotRange = 8.0f;   // Good range for cards
            _spotlight.LightColor = new Color(0.4f, 0.2f, 1.0f); // UV purple color
            _spotlight.LightEnergy = BlacklightIntensity;
            _spotlight.Visible = BlacklightEnabled;
        }
        
        // Add to group for easy finding
        AddToGroup("player");
        
        GD.Print("Controls: F = Toggle Blacklight, +/- = Adjust Intensity");
    }

    public override void _Input(InputEvent @event)
    {
        // Mouse look
        if (@event is InputEventMouseMotion motion)
        {
            float yawDelta = -motion.Relative.X * MouseSensitivity;
            RotateY(Mathf.DegToRad(yawDelta));

            _pitchDeg = Mathf.Clamp(_pitchDeg - motion.Relative.Y * MouseSensitivity, MinPitch, MaxPitch);
            _head.RotationDegrees = new Vector3(_pitchDeg, 0, 0);
        }
        
        // Blacklight controls
        if (@event is InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            switch (keyEvent.Keycode)
            {
                case Key.F:
                    ToggleBlacklight();
                    break;
                    
                case Key.Equal:
                case Key.Plus:
                    AdjustBlacklightIntensity(0.2f);
                    break;
                    
                case Key.Minus:
                    AdjustBlacklightIntensity(-0.2f);
                    break;
            }
        }
    }

    private void ToggleBlacklight()
    {
        BlacklightEnabled = !BlacklightEnabled;
        
        if (_spotlight != null)
        {
            _spotlight.Visible = BlacklightEnabled;
        }
        
        string status = BlacklightEnabled ? "ON" : "OFF";
        GD.Print($"🔦 Blacklight {status}");
    }

    private void AdjustBlacklightIntensity(float delta)
    {
        BlacklightIntensity = Mathf.Clamp(BlacklightIntensity + delta, 0.1f, 3.0f);
        
        if (_spotlight != null)
        {
            _spotlight.LightEnergy = BlacklightIntensity;
        }
        
        GD.Print($"💡 Blacklight Intensity: {BlacklightIntensity:F1}");
    }

    public override void _PhysicsProcess(double delta)
    {
        // Movement input
        Vector2 input = new Vector2(
            Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left"),
            Input.GetActionStrength("ui_down")  - Input.GetActionStrength("ui_up")
        ).Normalized();

        // Apply movement
        var vel = Velocity;
        Vector3 dir = (Transform.Basis.X * input.X) + (Transform.Basis.Z * input.Y);
        vel.X = dir.X * Speed;
        vel.Z = dir.Z * Speed;

        // Jumping
        if (IsOnFloor() && Input.IsActionJustPressed("ui_accept"))
            vel.Y = JumpVelocity;

        // Gravity
        float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
        vel.Y -= gravity * (float)delta;

        Velocity = vel;
        MoveAndSlide();
    }
}