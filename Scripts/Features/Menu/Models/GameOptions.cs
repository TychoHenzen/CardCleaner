using System;

namespace CardCleaner.Scripts.Features.Menu.Models;

/// <summary>
///     The player's options as plain values. Holds no Godot types, so every rule about valid
///     values is tested without an engine.
/// </summary>
public sealed record GameOptions(
    float MouseSensitivity,
    bool InvertY,
    float MasterVolume,
    float MusicVolume,
    float SfxVolume,
    bool Fullscreen,
    int ResolutionIndex,
    bool Vsync)
{
    public const float MinMouseSensitivity = 0.02f;
    public const float MaxMouseSensitivity = 0.5f;

    public static readonly GameOptions Defaults = new(0.1f, false, 1f, 1f, 1f, false, 0, true);

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
    public GameOptions Clamped() => new(
        ClampOrDefault(MouseSensitivity, Defaults.MouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity),
        InvertY,
        ClampOrDefault(MasterVolume, Defaults.MasterVolume, 0f, 1f),
        ClampOrDefault(MusicVolume, Defaults.MusicVolume, 0f, 1f),
        ClampOrDefault(SfxVolume, Defaults.SfxVolume, 0f, 1f),
        Fullscreen,
        Math.Clamp(ResolutionIndex, 0, Resolutions.Length - 1),
        Vsync);

    private static float ClampOrDefault(float value, float fallback, float min, float max) =>
        float.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
}
