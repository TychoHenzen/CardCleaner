using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Menu.Services;

/// <summary>
///     Reads and writes <see cref="GameOptions" /> as a ConfigFile. Load never throws: a missing,
///     unreadable or malformed file yields the defaults. Save writes a temp file and renames it over the target.
/// </summary>
// ASSUMPTION: registered under its own type (no interface), because a [Service] with no types names no
// registration; the applier and the menu resolve OptionsStore directly. Medium risk: revisit if a contract is wanted.
[Service(typeof(OptionsStore))]
public class OptionsStore
{
    private const string DefaultPath = "user://settings.cfg";
    private const string Section = "options";

    private readonly string _path;

    // The container builds the public constructor, and it resolves every parameter from the container.
    // A string path is not a service, so the path lives in an internal constructor that only tests call.
    public OptionsStore() : this(DefaultPath)
    {
    }

    internal OptionsStore(string path)
    {
        _path = path;
    }

    public GameOptions Load()
    {
        try
        {
            var file = new ConfigFile();
            var error = file.Load(_path);
            if (error != Error.Ok)
            {
                ILog.Warning($"Options file {_path} could not be read ({error}); using defaults");
                return GameOptions.Defaults;
            }

            var defaults = GameOptions.Defaults;
            return new GameOptions
            {
                MouseSensitivity = file.GetValue(Section, "mouse_sensitivity", defaults.MouseSensitivity).AsSingle(),
                InvertY = file.GetValue(Section, "invert_y", defaults.InvertY).AsBool(),
                MasterVolume = file.GetValue(Section, "master_volume", defaults.MasterVolume).AsSingle(),
                MusicVolume = file.GetValue(Section, "music_volume", defaults.MusicVolume).AsSingle(),
                SfxVolume = file.GetValue(Section, "sfx_volume", defaults.SfxVolume).AsSingle(),
                Fullscreen = file.GetValue(Section, "fullscreen", defaults.Fullscreen).AsBool(),
                ResolutionIndex = file.GetValue(Section, "resolution_index", defaults.ResolutionIndex).AsInt32(),
                Vsync = file.GetValue(Section, "vsync", defaults.Vsync).AsBool(),
            }.Clamped();
        }
        catch (System.Exception ex)
        {
            ILog.Warning($"Options file {_path} is malformed ({ex.Message}); using defaults");
            return GameOptions.Defaults;
        }
    }

    public void Save(GameOptions options)
    {
        var clean = options.Clamped();
        var file = new ConfigFile();
        file.SetValue(Section, "mouse_sensitivity", clean.MouseSensitivity);
        file.SetValue(Section, "invert_y", clean.InvertY);
        file.SetValue(Section, "master_volume", clean.MasterVolume);
        file.SetValue(Section, "music_volume", clean.MusicVolume);
        file.SetValue(Section, "sfx_volume", clean.SfxVolume);
        file.SetValue(Section, "fullscreen", clean.Fullscreen);
        file.SetValue(Section, "resolution_index", clean.ResolutionIndex);
        file.SetValue(Section, "vsync", clean.Vsync);

        var tempPath = _path + ".tmp";
        var saveError = file.Save(tempPath);
        if (saveError != Error.Ok)
        {
            ILog.Error($"Could not write options to {tempPath} ({saveError})");
            return;
        }

        var renameError = DirAccess.RenameAbsolute(
            ProjectSettings.GlobalizePath(tempPath),
            ProjectSettings.GlobalizePath(_path));
        if (renameError != Error.Ok)
            ILog.Error($"Could not replace {_path} with {tempPath} ({renameError})");
    }
}
