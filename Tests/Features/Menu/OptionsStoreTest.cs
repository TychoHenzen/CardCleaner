using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

[TestSuite]
[RequireGodotRuntime]
public class OptionsStoreTest
{
    private string _path = null!;

    [BeforeTest]
    public void Setup()
    {
        _path = $"user://test_options_{System.Guid.NewGuid():N}.cfg";
    }

    [AfterTest]
    public void Teardown()
    {
        DeleteIfExists(_path);
        DeleteIfExists(_path + ".tmp");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Save_then_Load_round_trips()
    {
        var options = new GameOptions
        {
            MouseSensitivity = 0.25f, InvertY = true, MasterVolume = 0.5f, MusicVolume = 0.75f, SfxVolume = 0.125f,
            Fullscreen = true, ResolutionIndex = 2, Vsync = false,
        };
        var store = new OptionsStore(_path);

        store.Save(options);
        var loaded = store.Load();

        Assertions.AssertThat(loaded).IsEqual(options);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_of_garbage_file_returns_defaults()
    {
        WriteText(_path, "not [ a cfg");

        var loaded = new OptionsStore(_path).Load();

        Assertions.AssertThat(loaded).IsEqual(GameOptions.Defaults);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_of_missing_file_returns_defaults()
    {
        var loaded = new OptionsStore(_path).Load();

        Assertions.AssertThat(loaded).IsEqual(GameOptions.Defaults);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Save_clamps_out_of_range_values()
    {
        var store = new OptionsStore(_path);

        store.Save(new GameOptions
        {
            MouseSensitivity = 9f, InvertY = false, MasterVolume = -1f, MusicVolume = 2f, SfxVolume = 0.5f,
            Fullscreen = false, ResolutionIndex = 99, Vsync = true,
        });
        var loaded = store.Load();

        Assertions.AssertThat(loaded).IsEqual(new GameOptions
        {
            MouseSensitivity = 0.5f, InvertY = false, MasterVolume = 0f, MusicVolume = 1f, SfxVolume = 0.5f,
            Fullscreen = false, ResolutionIndex = 3, Vsync = true,
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Save_after_garbage_load_repairs_the_file()
    {
        WriteText(_path, "not [ a cfg");
        var store = new OptionsStore(_path);
        Assertions.AssertThat(store.Load()).IsEqual(GameOptions.Defaults);

        var options = new GameOptions
        {
            MouseSensitivity = 0.3f, InvertY = true, MasterVolume = 0.2f, MusicVolume = 0.4f, SfxVolume = 0.6f,
            Fullscreen = false, ResolutionIndex = 1, Vsync = false,
        };
        store.Save(options);

        Assertions.AssertThat(store.Load()).IsEqual(options);
    }

    private static void WriteText(string path, string text)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(text);
    }

    private static void DeleteIfExists(string path)
    {
        if (FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }
}
