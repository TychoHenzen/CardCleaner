using Godot;
using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;

public partial class PlayerController : CharacterBody3D
{
    private Node3D _head;
    private SpotLight3D _spotlight;
    private float _pitchDeg = 0f;
    private IGameSettings _settings;
    private IInputService _inputService;
    
    private readonly Color BlacklightColor = new Color(0.4f, 0.2f, 1.0f); // UV purple
    private readonly Color FlashlightColor = new Color(1.0f, 0.95f, 0.8f); // Warm white

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _spotlight = GetNode<SpotLight3D>("Head/Camera3D/SpotLight3D");
        
        ServiceLocator.Get<IGameSettings>(settings => 
        {
            _settings = settings;
            ConfigureSpotlight();
        });
        
        ServiceLocator.Get<IInputService>(input =>
        {
            _inputService = input;
            RegisterInputActions();
        });
        // Add to group for easy finding
        AddToGroup("player");
        
        GD.Print("Controls: F = Toggle Blacklight, +/- = Adjust Intensity");
    }

    private void RegisterInputActions()
    {
        // Register light cycling control
        _inputService.RegisterAction("cycle_light", Key.F, CycleLightMode);
        _inputService.RegisterAction("increase_light_intensity", Key.Plus, () => AdjustLightIntensity(0.2f));
        _inputService.RegisterAction("decrease_light_intensity", Key.Minus, () => AdjustLightIntensity(-0.2f));
        _inputService.RegisterAction("increase_light_intensity_alt", Key.Equal, () => AdjustLightIntensity(0.2f));
        
        // Subscribe to mouse movement
        _inputService.MouseMoved += OnMouseMoved;
        
        GD.Print("[PlayerController] Registered light controls and mouse input");
    }
    
    public override void _ExitTree()
    {
        // Clean up input registrations
        _inputService?.UnregisterAllActions(this);
        
        // Unsubscribe from events
        if (_inputService != null)
            _inputService.MouseMoved -= OnMouseMoved;
    }

    private void ConfigureSpotlight()
    {
        if (_spotlight != null)
        {
            _spotlight.SpotAngle = 60.0f;  // Wide cone
            _spotlight.SpotRange = 8.0f;   // Good range for cards
            ApplyLightMode();
        }
    }
    private void CycleLightMode()
    {
        // Cycle through the three states
        _settings.CurrentLightMode = _settings.CurrentLightMode switch
        {
            LightMode.Off => LightMode.Blacklight,
            LightMode.Blacklight => LightMode.Flashlight,
            _ => LightMode.Off
        };
        
        ApplyLightMode();
        
        string status = _settings.CurrentLightMode switch
        {
            LightMode.Off => "OFF",
            LightMode.Blacklight => "BLACKLIGHT",
            LightMode.Flashlight => "FLASHLIGHT",
            _ => "UNKNOWN"
        };
        
        string emoji = _settings.CurrentLightMode switch
        {
            LightMode.Off => "⚫",
            LightMode.Blacklight => "🟣",
            LightMode.Flashlight => "🔦",
            _ => "❓"
        };
        
        GD.Print($"{emoji} Light Mode: {status}");
    }


    private void ApplyLightMode()
    {
        if (_spotlight == null) return;

        switch (_settings.CurrentLightMode)
        {
            case LightMode.Off:
                _spotlight.Visible = false;
                break;
                
            case LightMode.Blacklight:
                _spotlight.Visible = true;
                _spotlight.LightColor = BlacklightColor;
                _spotlight.LightEnergy = _settings.LightIntensity;
                break;
                
            case LightMode.Flashlight:
                _spotlight.Visible = true;
                _spotlight.LightColor = FlashlightColor;
                _spotlight.LightEnergy = _settings.LightIntensity;
                break;
        }
    }

    private void AdjustLightIntensity(float delta)
    {
        _settings.LightIntensity = Mathf.Clamp(_settings.LightIntensity + delta, 0.1f, 5.0f);
        
        // Only apply if light is currently on
        if (_settings.CurrentLightMode != LightMode.Off)
        {
            ApplyLightMode();
            
            string modeText = _settings.CurrentLightMode == LightMode.Blacklight ? "Blacklight" : "Flashlight";
            GD.Print($"💡 {modeText} Intensity: {_settings.LightIntensity:F1}");
        }
        else
        {
            GD.Print($"💡 Light Intensity set to: {_settings.LightIntensity:F1} (currently off)");
        }
    }
    
    private void OnMouseMoved(Vector2 delta)
    {
        float yawDelta = -delta.X * _settings.MouseSensitivity;
        RotateY(Mathf.DegToRad(yawDelta));

        _pitchDeg = Mathf.Clamp(_pitchDeg - delta.Y * _settings.MouseSensitivity, _settings.MinPitch, _settings.MaxPitch);
        _head.RotationDegrees = new Vector3(_pitchDeg, 0, 0);
    }
    public override void _PhysicsProcess(double delta)
    {
        if (_settings == null) return;
        // Movement input
        Vector2 input = new Vector2(
            Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left"),
            Input.GetActionStrength("ui_down")  - Input.GetActionStrength("ui_up")
        ).Normalized();

        // Apply movement
        var vel = Velocity;
        Vector3 dir = Transform.Basis.X * input.X + Transform.Basis.Z * input.Y;
        vel.X = dir.X * _settings.MovementSpeed;
        vel.Z = dir.Z * _settings.MovementSpeed;

        // Jumping
        if (IsOnFloor() && Input.IsActionJustPressed("ui_accept"))
            vel.Y = _settings.JumpVelocity;

        // Gravity
        float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
        vel.Y -= gravity * (float)delta;

        Velocity = vel;
        MoveAndSlide();
    }
}