using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Data;

/// <summary>
///     Configurable game settings that can be set in the editor.
///     Add this node to a scene and configure values via Export properties.
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IGameSettings))]
public partial class GameSettings : Node, IGameSettings
{
    // Default values as constants
    private const float DefaultMovementSpeed = 5.0f;
    private const float DefaultJumpVelocity = 10.0f;
    private const float DefaultMouseSensitivity = 0.1f;
    private const float DefaultMinPitch = -80f;
    private const float DefaultMaxPitch = 80f;
    private const LightMode DefaultLightMode = LightMode.Off;
    private const float DefaultLightIntensity = 1.5f;

    [ExportGroup("Player Movement")]
    [Export]
    public float MovementSpeed { get; set; } = DefaultMovementSpeed;

    [Export] public float JumpVelocity { get; set; } = DefaultJumpVelocity;

    [ExportGroup("Camera Controls")]
    [Export]
    public float MouseSensitivity { get; set; } = DefaultMouseSensitivity;

    [Export] public float MinPitch { get; set; } = DefaultMinPitch;
    [Export] public float MaxPitch { get; set; } = DefaultMaxPitch;

    [ExportGroup("Blacklight")] public LightMode CurrentLightMode { get; set; } = DefaultLightMode;

    public float LightIntensity { get; set; } = DefaultLightIntensity;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(MovementSpeed) => true,
            nameof(JumpVelocity) => true,
            nameof(MouseSensitivity) => true,
            nameof(MinPitch) => true,
            nameof(MaxPitch) => true,
            nameof(CurrentLightMode) => true,
            nameof(LightIntensity) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(MovementSpeed) => DefaultMovementSpeed,
            nameof(JumpVelocity) => DefaultJumpVelocity,
            nameof(MouseSensitivity) => DefaultMouseSensitivity,
            nameof(MinPitch) => DefaultMinPitch,
            nameof(MaxPitch) => DefaultMaxPitch,
            nameof(CurrentLightMode) => (int)DefaultLightMode,
            nameof(LightIntensity) => DefaultLightIntensity,
            _ => base._PropertyGetRevert(property)
        };
    }
}