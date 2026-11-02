using System;

namespace CardCleaner.Scripts.Features.Menu.Models;

/// <summary>
///     The player's options as plain values. Holds no Godot types, so every rule about valid
///     values is tested without an engine.
/// </summary>
public sealed record GameOptions
{
    public const float MinMouseSensitivity = 0.02f;
    public const float MaxMouseSensitivity = 0.5f;

    public float MouseSensitivity { get; init; } = 0.1f;
    public bool InvertY { get; init; }
    public float MasterVolume { get; init; } = 1f;
    public float MusicVolume { get; init; } = 1f;
    public float SfxVolume { get; init; } = 1f;
    public bool Fullscreen { get; init; }
    public int ResolutionIndex { get; init; }
    public bool Vsync { get; init; } = true;

    public static readonly GameOptions Defaults = new();

    public static readonly (int Width, int Height)[] Resolutions =
    [
        (1280, 720),
        (1600, 900),
        (1920, 1080),
        (2560, 1440),
    ];

    /// <summary>
    ///     Returns a copy with every field inside its allowed range. A NaN value becomes the
    ///     default for that field; the other fields are clamped.
    /// </summary>
    public GameOptions Clamped() => new()
    {
        MouseSensitivity = ClampOrDefault(
            MouseSensitivity, Defaults.MouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity),
        InvertY = InvertY,
        MasterVolume = ClampOrDefault(MasterVolume, Defaults.MasterVolume, 0f, 1f),
        MusicVolume = ClampOrDefault(MusicVolume, Defaults.MusicVolume, 0f, 1f),
        SfxVolume = ClampOrDefault(SfxVolume, Defaults.SfxVolume, 0f, 1f),
        Fullscreen = Fullscreen,
        ResolutionIndex = Math.Clamp(ResolutionIndex, 0, Resolutions.Length - 1),
        Vsync = Vsync,
    };

    private static float ClampOrDefault(float value, float fallback, float min, float max) =>
        float.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
}
