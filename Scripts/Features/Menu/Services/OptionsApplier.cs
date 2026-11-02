using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Menu.Services;

/// <summary>
///     Pushes a <see cref="GameOptions" /> value into the engine. <see cref="Apply" /> sets the engine-global parts:
///     the audio bus volumes directly, and the window mode, size and vsync as a <see cref="DisplayPlan" /> handed to
///     a display sink (the engine calls by default). It works in any scene. <see cref="ApplyMouse" /> sets the
///     mouse parts on the <see cref="IGameSettings" /> of the current scene, when that scene has one.
/// </summary>
// ASSUMPTION: registered under its own type (no interface), matching OptionsStore; the menu resolves it directly.
// Medium risk: revisit if a contract is wanted.
[Service(typeof(OptionsApplier))]
public class OptionsApplier
{
    private const string MasterBus = "Master";
    private const string MusicBus = "Music";
    private const string SfxBus = "SFX";

    private readonly Action<DisplayPlan, Window> _displaySink;

    // The container builds the public constructor, and it resolves every parameter from the container. A delegate
    // is not a service, so the sink lives in an internal constructor that only tests call.
    public OptionsApplier() : this(ApplyToEngine)
    {
    }

    internal OptionsApplier(Action<DisplayPlan, Window> displaySink)
    {
        _displaySink = displaySink;
    }

    /// <summary>
    ///     The engine-facing display state for <paramref name="options" />. Options are clamped first, so an
    ///     out-of-range resolution index gives the nearest allowed size. Fullscreen has no size.
    /// </summary>
    public static DisplayPlan DisplayFor(GameOptions options)
    {
        var clean = options.Clamped();
        var mode = clean.Fullscreen ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed;
        Vector2I? size = null;
        if (!clean.Fullscreen)
        {
            var (width, height) = GameOptions.Resolutions[clean.ResolutionIndex];
            size = new Vector2I(width, height);
        }

        var vsync = clean.Vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled;
        return new DisplayPlan(mode, size, vsync);
    }

    public void Apply(GameOptions options, Window window)
    {
        ApplyBusVolume(MasterBus, options.MasterVolume);
        ApplyBusVolume(MusicBus, options.MusicVolume);
        ApplyBusVolume(SfxBus, options.SfxVolume);

        _displaySink(DisplayFor(options), window);
    }

    public void ApplyMouse(GameOptions options, IGameSettings settings)
    {
        settings.MouseSensitivity = options.MouseSensitivity;
        settings.InvertY = options.InvertY;
    }

    private static void ApplyToEngine(DisplayPlan plan, Window window)
    {
        DisplayServer.WindowSetMode(plan.Mode);
        if (plan.Size is { } size)
            window.Size = size;

        DisplayServer.WindowSetVsyncMode(plan.VSync);
    }

    private static void ApplyBusVolume(string busName, float volume)
    {
        var index = AudioServer.GetBusIndex(busName);
        if (index < 0)
        {
            ILog.Warning($"Audio bus '{busName}' is missing from the bus layout; its volume was not applied");
            return;
        }

        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(volume));
    }
}
