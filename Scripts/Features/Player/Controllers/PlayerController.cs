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

    private readonly StuckDetector _stuckDetector = new();
    private float _gravity;
    private Node3D? _head;
    private IInputService? _inputService;
    private float _pitchDeg;
    private IPlayerResetService? _playerResetService;
    private ISafePositionTracker? _safePositionTracker;
    private IGameSettings? _settings;
    private PlayerLightController? _light;
    private float _timeSinceLastSafeRecord;

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
        _light = new PlayerLightController(GetNode<SpotLight3D>("Head/Camera3D/SpotLight3D"));

        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

        ServiceLocator.Get<IGameSettings>(settings =>
        {
            _settings = settings;
            _light?.UseSettings(settings);
            _light?.Configure();
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
        _inputService.RegisterAction(this, "cycle_light", Key.F, () => _light?.Cycle());
        System.Action brighter = () => _light?.AdjustIntensity(0.2f);
        _inputService.RegisterAction(this, "increase_light_intensity", Key.Plus, brighter);
        _inputService.RegisterAction(this, "decrease_light_intensity", Key.Minus, () => _light?.AdjustIntensity(-0.2f));
        _inputService.RegisterAction(this, "increase_light_intensity_alt", Key.Equal, brighter);

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

    private void ApplyLightMode() => _light?.Apply();

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
        if (!_stuckDetector.Update(GlobalPosition, delta, hasMovementInput))
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
