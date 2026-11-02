using System;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Controllers;
using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

/// <summary>
/// The shop's startup node carries the saved mouse options into the scene's GameSettings. The options file lives at a
/// throw-away user:// path that the teardown deletes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MouseOptionsStartupTest
{
    private string _path = null!;
    private GameSettings _settings = null!;
    private MouseOptionsStartup? _node;

    [BeforeTest]
    public void Setup()
    {
        // Reinitialize clears the container and composes the autoload again; the registrations below replace it.
        TestServices.Reinitialize();
        _path = $"user://test_mouse_startup_{Guid.NewGuid():N}.cfg";
        _settings = new GameSettings();
        TestServices.Container.RegisterSingleton<IGameSettings>(_settings);
        TestServices.Container.RegisterSingleton<OptionsApplier>(new OptionsApplier());
    }

    [AfterTest]
    public void Teardown()
    {
        _node?.Free();
        _settings.Free();
        TestServices.Reinitialize();
        if (FileAccess.FileExists(_path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_path));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Saved_mouse_options_are_applied_to_the_settings_on_startup()
    {
        var store = new OptionsStore(_path);
        store.Save(GameOptions.Defaults with { MouseSensitivity = 0.3f, InvertY = true });
        TestServices.Container.RegisterSingleton<OptionsStore>(store);

        AddStartupNode();

        Assertions.AssertFloat(_settings.MouseSensitivity).IsEqual(0.3f);
        Assertions.AssertThat(_settings.InvertY).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Missing_file_keeps_defaults_on_startup()
    {
        TestServices.Container.RegisterSingleton<OptionsStore>(new OptionsStore(_path));

        AddStartupNode();

        Assertions.AssertFloat(_settings.MouseSensitivity).IsEqual(0.1f);
        Assertions.AssertThat(_settings.InvertY).IsFalse();
    }

    private void AddStartupNode()
    {
        _node = new MouseOptionsStartup();
        TestServices.Inject(_node);
        Assertions.AddNode(_node, autoFree: false);
    }
}
