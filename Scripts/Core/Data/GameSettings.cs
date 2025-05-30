using CardCleaner.Scripts.Core.Enum;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Data;

/// <summary>
///     Configurable game settings that can be set in the editor.
///     Add this node to a scene and configure values via Export properties.
/// </summary>
public partial class GameSettings : Node, IGameSettings
{
    [ExportGroup("Player Movement")]
    [Export]
    public float MovementSpeed { get; set; } = 5.0f;

    [Export] public float JumpVelocity { get; set; } = 10.0f;

    [ExportGroup("Camera Controls")]
    [Export]
    public float MouseSensitivity { get; set; } = 0.1f;

    [Export] public float MinPitch { get; set; } = -80f;
    [Export] public float MaxPitch { get; set; } = 80f;

    [ExportGroup("Blacklight")] public LightMode CurrentLightMode { get; set; } = LightMode.Off;

    public float LightIntensity { get; set; } = 1.5f;
}