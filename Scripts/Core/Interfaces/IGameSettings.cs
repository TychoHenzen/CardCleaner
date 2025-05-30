using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IGameSettings
{
    // Player movement settings
    float MovementSpeed { get; }
    float JumpVelocity { get; }
    
    // Camera/Mouse settings
    float MouseSensitivity { get; }
    float MinPitch { get; }
    float MaxPitch { get; }
    
    // Blacklight settings
    LightMode CurrentLightMode { get; set; }
    float LightIntensity { get; set; }
    
}