using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Menu.Services;

/// <summary>
///     Pushes a <see cref="GameOptions" /> value into the engine. <see cref="Apply" /> sets the engine-global parts
///     (audio bus volumes, window mode, size and vsync) and works in any scene. <see cref="ApplyMouse" /> sets the
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

    public void Apply(GameOptions options, Window window)
    {
        ApplyBusVolume(MasterBus, options.MasterVolume);
        ApplyBusVolume(MusicBus, options.MusicVolume);
        ApplyBusVolume(SfxBus, options.SfxVolume);

        DisplayServer.WindowSetMode(options.Fullscreen
            ? DisplayServer.WindowMode.ExclusiveFullscreen
            : DisplayServer.WindowMode.Windowed);
        if (!options.Fullscreen)
        {
            var (width, height) = GameOptions.Resolutions[options.ResolutionIndex];
            window.Size = new Vector2I(width, height);
        }

        DisplayServer.WindowSetVsyncMode(options.Vsync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
    }

    public void ApplyMouse(GameOptions options, IGameSettings settings)
    {
        settings.MouseSensitivity = options.MouseSensitivity;
        settings.InvertY = options.InvertY;
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
