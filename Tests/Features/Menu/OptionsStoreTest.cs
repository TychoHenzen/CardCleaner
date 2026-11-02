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
        var options = new GameOptions(0.25f, true, 0.5f, 0.75f, 0.125f, true, 2, false);
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

        store.Save(new GameOptions(9f, false, -1f, 2f, 0.5f, false, 99, true));
        var loaded = store.Load();

        Assertions.AssertThat(loaded).IsEqual(new GameOptions(0.5f, false, 0f, 1f, 0.5f, false, 3, true));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Save_after_garbage_load_repairs_the_file()
    {
        WriteText(_path, "not [ a cfg");
        var store = new OptionsStore(_path);
        Assertions.AssertThat(store.Load()).IsEqual(GameOptions.Defaults);

        var options = new GameOptions(0.3f, true, 0.2f, 0.4f, 0.6f, false, 1, false);
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
