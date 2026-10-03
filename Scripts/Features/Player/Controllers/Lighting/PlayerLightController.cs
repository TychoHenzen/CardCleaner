using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Player.Controllers;

/// <summary>
/// Drives the player spotlight from the game settings: mode cycling, intensity changes and color.
/// </summary>
internal sealed class PlayerLightController
{
    private readonly Color BlacklightColor = new(0.4f, 0.2f, 1.0f); // UV purple
    private readonly Color FlashlightColor = new(1.0f, 0.95f, 0.8f); // Warm white
    private readonly SpotLight3D? _spotlight;
    private IGameSettings? _settings;

    internal PlayerLightController(SpotLight3D? spotlight)
    {
        _spotlight = spotlight;
    }

    internal void UseSettings(IGameSettings settings)
    {
        _settings = settings;
    }

    internal void Configure()
    {
        if (_spotlight == null)
            return;

        _spotlight.SpotAngle = 60.0f; // Wide cone
        _spotlight.SpotRange = 8.0f; // Good range for cards
        Apply();
    }

    internal void Cycle()
    {
        if (_settings == null) return;
        // Cycle through the three states
        _settings.CurrentLightMode = _settings.CurrentLightMode switch
        {
            LightMode.Off => LightMode.Blacklight,
            LightMode.Blacklight => LightMode.Flashlight,
            _ => LightMode.Off
        };

        Apply();

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

    internal void Apply()
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

    internal void AdjustIntensity(float delta)
    {
        if (_settings == null) return;
        _settings.LightIntensity = Mathf.Clamp(_settings.LightIntensity + delta, 0.1f, 5.0f);

        // Only apply if light is currently on
        if (_settings.CurrentLightMode != LightMode.Off)
        {
            Apply();

            var modeText = _settings.CurrentLightMode == LightMode.Blacklight ? "Blacklight" : "Flashlight";
            ILog.Print($"💡 {modeText} Intensity: {_settings.LightIntensity:F1}");
        }
        else
        {
            ILog.Print($"💡 Light Intensity set to: {_settings.LightIntensity:F1} (currently off)");
        }
    }
}
