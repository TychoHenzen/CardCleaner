using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Player.Controllers;

public partial class PlayerController : CharacterBody3D, ISaveable
{
    private const float SafePositionRecordInterval = 1.0f; // Record safe position every 1 second
    private const float OutOfBoundsYThreshold = -50f; // Player falls below this Y and triggers reset
    private const float StuckDetectionWindow = 5.0f; // Time window to detect stuck state
    private const float StuckMovementThreshold = 0.5f; // Minimum cumulative movement expected in window

    private readonly Color BlacklightColor = new(0.4f, 0.2f, 1.0f); // UV purple
    private readonly Color FlashlightColor = new(1.0f, 0.95f, 0.8f); // Warm white
    private float _cumulativeMovement;
    private float _gravity;
    private Node3D? _head;
    private IInputService? _inputService;

    // Stuck detection tracking
    private Vector3 _lastPositionForStuck;
    private float _pitchDeg;
    private IPlayerResetService? _playerResetService;
    private ISafePositionTracker? _safePositionTracker;
    private IGameSettings? _settings;
    private SpotLight3D? _spotlight;
    private float _stuckTimer;
    private float _timeSinceLastSafeRecord;
    private float _timeWithMovementInput;

    public StringName UniqueID => "player";

    public void Save(NodeSave save)
    {
        save.SetOrAddProperty("position", GlobalPosition);
        save.SetOrAddProperty("rotation", GlobalRotation);
        save.SetOrAddProperty("pitchDeg", _pitchDeg);

        if (_settings != null)
        {
            save.SetOrAddProperty("lightMode", (int)_settings.CurrentLightMode);
            save.SetOrAddProperty("lightIntensity", _settings.LightIntensity);
        }
    }

    public void Load(NodeSave save)
    {
        if (save.TryGetProperty<Vector3>("position", out var pos))
            GlobalPosition = pos;

        if (save.TryGetProperty<Vector3>("rotation", out var rot))
            GlobalRotation = rot;

        if (save.TryGetProperty<float>("pitchDeg", out var pitch))
            _pitchDeg = pitch;

        if (_settings != null)
        {
            if (save.TryGetProperty<int>("lightMode", out var mode))
                _settings.CurrentLightMode = (LightMode)mode;

            if (save.TryGetProperty<float>("lightIntensity", out var intensity))
                _settings.LightIntensity = intensity;
        }

        // Reapply settings after load
        CallDeferred(MethodName.ApplyLightMode);
    }

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _spotlight = GetNode<SpotLight3D>("Head/Camera3D/SpotLight3D");

        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

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

        ServiceLocator.Get<ISafePositionTracker>(tracker =>
        {
            _safePositionTracker = tracker;
            // Record initial spawn position
            _safePositionTracker.SetSpawnPosition(GlobalPosition);
            _safePositionTracker.RecordSafePosition(GlobalPosition);
        });

        ServiceLocator.Get<IPlayerResetService>(resetService => _playerResetService = resetService);

        // Add to group for easy finding
        AddToGroup("player");

        ILog.Print("Controls: F = Toggle Blacklight, +/- = Adjust Intensity");
    }

    private void RegisterInputActions()
    {
        if (_inputService == null) return;
        // Register light cycling control
        _inputService.RegisterAction(this, "cycle_light", Key.F, CycleLightMode);
        _inputService.RegisterAction(this, "increase_light_intensity", Key.Plus, () => AdjustLightIntensity(0.2f));
        _inputService.RegisterAction(this, "decrease_light_intensity", Key.Minus, () => AdjustLightIntensity(-0.2f));
        _inputService.RegisterAction(this, "increase_light_intensity_alt", Key.Equal, () => AdjustLightIntensity(0.2f));

        // Register safety reset action (R key)
        _inputService.RegisterAction(this, "player_reset", Key.R, OnResetRequested);

        // Subscribe to mouse movement
        _inputService.MouseMoved += OnMouseMoved;

        ILog.Print("Registered light controls and mouse input");
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
        if (_spotlight == null)
            return;

        _spotlight.SpotAngle = 60.0f; // Wide cone
        _spotlight.SpotRange = 8.0f; // Good range for cards
        ApplyLightMode();
    }

    private void CycleLightMode()
    {
        if (_settings == null) return;
        // Cycle through the three states
        _settings.CurrentLightMode = _settings.CurrentLightMode switch
        {
            LightMode.Off => LightMode.Blacklight,
            LightMode.Blacklight => LightMode.Flashlight,
            _ => LightMode.Off
        };

        ApplyLightMode();

        var status = _settings.CurrentLightMode switch
        {
            LightMode.Off => "OFF",
            LightMode.Blacklight => "BLACKLIGHT",
            LightMode.Flashlight => "FLASHLIGHT",
            _ => "UNKNOWN"
        };

        var emoji = _settings.CurrentLightMode switch
        {
            LightMode.Off => "⚫",
            LightMode.Blacklight => "🟣",
            LightMode.Flashlight => "🔦",
            _ => "❓"
        };

        ILog.Print($"{emoji} Light Mode: {status}");
    }


    private void ApplyLightMode()
    {
        if (_spotlight == null || _settings == null) return;

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
        if (_settings == null) return;
        _settings.LightIntensity = Mathf.Clamp(_settings.LightIntensity + delta, 0.1f, 5.0f);

        // Only apply if light is currently on
        if (_settings.CurrentLightMode != LightMode.Off)
        {
            ApplyLightMode();

            var modeText = _settings.CurrentLightMode == LightMode.Blacklight ? "Blacklight" : "Flashlight";
            ILog.Print($"💡 {modeText} Intensity: {_settings.LightIntensity:F1}");
        }
        else
        {
            ILog.Print($"💡 Light Intensity set to: {_settings.LightIntensity:F1} (currently off)");
        }
    }

    private void OnMouseMoved(Vector2 delta)
    {
        if (_settings == null || _head == null) return;
        var yawDelta = -delta.X * _settings.MouseSensitivity;
        RotateY(Mathf.DegToRad(yawDelta));

        _pitchDeg = Mathf.Clamp(_pitchDeg - delta.Y * _settings.MouseSensitivity, _settings.MinPitch,
            _settings.MaxPitch);
        _head.RotationDegrees = new Vector3(_pitchDeg, 0, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_settings == null) return;
        // Movement input
        var input = new Vector2(
            Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left"),
            Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up")
        ).Normalized();

        // Apply movement
        var vel = Velocity;
        var dir = Transform.Basis.X * input.X + Transform.Basis.Z * input.Y;
        vel.X = dir.X * _settings.MovementSpeed;
        vel.Z = dir.Z * _settings.MovementSpeed;

        // Jumping
        if (IsOnFloor() && Input.IsActionJustPressed("ui_accept"))
            vel.Y = _settings.JumpVelocity;

        // Gravity
        vel.Y -= _gravity * (float)delta;

        Velocity = vel;
        MoveAndSlide();

        // Record safe position periodically when on floor
        RecordSafePositionIfOnGround((float)delta);

        // Check for out-of-bounds and auto-reset
        CheckOutOfBounds();

        // Check for stuck state (only when player is trying to move)
        CheckStuck((float)delta, input.LengthSquared() > 0.01f);
    }

    private void RecordSafePositionIfOnGround(float delta)
    {
        if (_safePositionTracker == null || !IsOnFloor())
            return;

        _timeSinceLastSafeRecord += delta;
        if (_timeSinceLastSafeRecord >= SafePositionRecordInterval)
        {
            _safePositionTracker.RecordSafePosition(GlobalPosition);
            _timeSinceLastSafeRecord = 0f;
        }
    }

    private void CheckOutOfBounds()
    {
        if (GlobalPosition.Y >= OutOfBoundsYThreshold)
            return;

        if (_playerResetService == null || _playerResetService.IsOnCooldown)
            return;

        ILog.Warning($"Player fell out of bounds (Y={GlobalPosition.Y:F1}), triggering safety reset");
        _playerResetService.ResetPlayerToSafety();
    }

    private void CheckStuck(float delta, bool hasMovementInput)
    {
        // Track movement delta
        var positionDelta = GlobalPosition.DistanceTo(_lastPositionForStuck);
        _cumulativeMovement += positionDelta;
        _lastPositionForStuck = GlobalPosition;

        // Only count time when player is actively trying to move
        if (hasMovementInput)
            _timeWithMovementInput += delta;

        _stuckTimer += delta;

        // Check if detection window has elapsed
        if (_stuckTimer < StuckDetectionWindow)
            return;

        // Only trigger stuck if player was trying to move for most of the window
        // and didn't actually move much
        var wasActivelyTryingToMove = _timeWithMovementInput > StuckDetectionWindow * 0.8f;
        var isStuck = wasActivelyTryingToMove && _cumulativeMovement < StuckMovementThreshold;

        // Reset tracking for next window
        _stuckTimer = 0f;
        _cumulativeMovement = 0f;
        _timeWithMovementInput = 0f;

        if (!isStuck)
            return;

        if (_playerResetService == null || _playerResetService.IsOnCooldown)
            return;

        ILog.Warning("Player appears stuck (minimal movement despite input), triggering safety reset");
        _playerResetService.ResetPlayerToSafety();
    }

    private void OnResetRequested()
    {
        if (_playerResetService == null)
        {
            ILog.Warning("Player reset service not available");
            return;
        }

        ILog.Print("Manual reset requested by player (R key)");
        _playerResetService.ResetPlayerToSafety();
    }

    /// <summary>
    ///     Teleports the player to the specified position and resets movement state.
    ///     Used for safety resets when player gets stuck or falls out of bounds.
    /// </summary>
    /// <param name="position">The world position to teleport to</param>
    public void ResetToPosition(Vector3 position)
    {
        GlobalPosition = position;
        Velocity = Vector3.Zero;
        ILog.Print($"Player reset to position: {position}");
    }
}
