using System;
using System.Collections.Generic;
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
/// The options screen instanced from OptionsMenu.tscn. Each case uses a throw-away user:// options file. The teardown
/// restores the audio buses and the window size that the screen changes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OptionsMenuSceneTest
{
    private const string ScenePath = "res://Scenes/UI/OptionsMenu.tscn";

    private string _path = null!;
    private Window _window = null!;
    private Vector2I _originalWindowSize;
    private OptionsMenuController? _node;
    private GameSettings? _settings;
    private List<DisplayPlan> _plans = new();

    [BeforeTest]
    public void Setup()
    {
        // Reinitialize clears the container and composes the autoload again; the registrations below replace it.
        TestServices.Reinitialize();
        _path = $"user://test_options_menu_{Guid.NewGuid():N}.cfg";
        TestServices.Container.RegisterSingleton<OptionsStore>(new OptionsStore(_path));
        TestServices.Container.RegisterSingleton<OptionsApplier>(new OptionsApplier());

        _window = ((SceneTree)Engine.GetMainLoop()).Root;
        _originalWindowSize = _window.Size;
        _window.Size = new Vector2I(1280, 720);
    }

    [AfterTest]
    public void Teardown()
    {
        _node?.Free();
        _node = null;
        _settings?.Free();
        _settings = null;
        // Restore the buses through a sink that does not touch the window.
        new OptionsApplier(static (_, _) => { }).Apply(GameOptions.Defaults, _window);
        _window.Size = _originalWindowSize;
        TestServices.Reinitialize();
        if (FileAccess.FileExists(_path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_path));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Changing_master_slider_to_half_writes_the_store_and_sets_the_Master_bus()
    {
        AddScreen();

        GetSlider("Layout/MasterSlider").Value = 0.5;

        Assertions.AssertFloat(new OptionsStore(_path).Load().MasterVolume).IsEqual(0.5f);
        var masterDb = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"));
        Assertions.AssertFloat(masterDb).IsEqualApprox(-6.02f, 0.05f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Back_emits_Closed()
    {
        AddScreen();
        var closed = false;
        _node!.Closed += () => closed = true;

        GetNode<Button>("Layout/BackButton").EmitSignal(BaseButton.SignalName.Pressed);

        Assertions.AssertBool(closed).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Mouse_change_reaches_IGameSettings_when_registered()
    {
        _settings = new GameSettings();
        TestServices.Container.RegisterSingleton<IGameSettings>(_settings);
        AddScreen();

        GetSlider("Layout/SensitivitySlider").Value = 0.3;

        Assertions.AssertFloat(_settings.MouseSensitivity).IsEqual(0.3f);
        Assertions.AssertFloat(new OptionsStore(_path).Load().MouseSensitivity).IsEqual(0.3f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Mouse_change_without_IGameSettings_only_saves()
    {
        AddScreen();

        GetSlider("Layout/SensitivitySlider").Value = 0.3;

        Assertions.AssertFloat(new OptionsStore(_path).Load().MouseSensitivity).IsEqual(0.3f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Controls_start_from_the_stored_options_without_saving()
    {
        new OptionsStore(_path).Save(GameOptions.Defaults with { MasterVolume = 0.25f });
        var before = FileAccess.GetFileAsString(_path);

        AddScreen();

        Assertions.AssertFloat((float)GetSlider("Layout/MasterSlider").Value).IsEqual(0.25f);
        Assertions.AssertString(FileAccess.GetFileAsString(_path)).IsEqual(before);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Controls_start_without_sending_a_display_plan()
    {
        UseRecorder();

        AddScreen();

        Assertions.AssertInt(_plans.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Toggling_fullscreen_sends_ExclusiveFullscreen_live()
    {
        UseRecorder();
        AddScreen();

        GetNode<CheckButton>("Layout/FullscreenCheck").ButtonPressed = true;

        Assertions.AssertInt(_plans.Count).IsEqual(1);
        Assertions.AssertBool(_plans[^1].Mode == DisplayServer.WindowMode.ExclusiveFullscreen).IsTrue();
        Assertions.AssertBool(_plans[^1].Size.HasValue).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Choosing_a_resolution_sends_that_size_live()
    {
        UseRecorder();
        AddScreen();
        var resolution = GetNode<OptionButton>("Layout/ResolutionOption");

        resolution.Select(2);
        resolution.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);

        Assertions.AssertInt(_plans.Count).IsEqual(1);
        Assertions.AssertBool(_plans[^1].Mode == DisplayServer.WindowMode.Windowed).IsTrue();
        Assertions.AssertThat(_plans[^1].Size).IsEqual(new Vector2I(1920, 1080));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Toggling_vsync_off_sends_Disabled_live()
    {
        UseRecorder();
        AddScreen();

        GetNode<CheckButton>("Layout/VsyncCheck").ButtonPressed = false;

        Assertions.AssertInt(_plans.Count).IsEqual(1);
        Assertions.AssertBool(_plans[^1].VSync == DisplayServer.VSyncMode.Disabled).IsTrue();
    }

    // Registers an applier that records each display plan instead of changing the window.
    private void UseRecorder()
    {
        _plans = new List<DisplayPlan>();
        TestServices.Container.RegisterSingleton<OptionsApplier>(new OptionsApplier((plan, _) => _plans.Add(plan)));
    }

    private T GetNode<T>(string path) where T : Node => _node!.GetNode<T>(path);

    private HSlider GetSlider(string path) => GetNode<HSlider>(path);

    private void AddScreen()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        _node = (OptionsMenuController)packed.Instantiate();
        TestServices.Inject(_node);
        Assertions.AddNode(_node, autoFree: false);
    }
}
