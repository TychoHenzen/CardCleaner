using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Menu.Controllers;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Controllers;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

/// <summary>
/// The Options and Quit to menu entries of PauseMenu.tscn, with the real scene instanced. The save command is a fake
/// that reports the result each case needs; the scene change is captured instead of run. The teardown unpauses the
/// tree and frees the scene; each case uses a throw-away user:// options file.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PauseMenuEntriesSceneTest
{
    private const string PauseMenuScenePath = "res://Scenes/UI/PauseMenu.tscn";
    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";

    private string _optionsPath = null!;
    private Node? _root;

    [BeforeTest]
    public void Setup()
    {
        // Reinitialize clears the container and composes the autoload again; the registrations in each case replace it.
        TestServices.Reinitialize();
        _optionsPath = $"user://test_pause_entries_{Guid.NewGuid():N}.cfg";
        TestServices.Container.RegisterSingleton<OptionsStore>(new OptionsStore(_optionsPath));
        TestServices.Container.RegisterSingleton<OptionsApplier>(new OptionsApplier());
    }

    [AfterTest]
    public void Teardown()
    {
        (Engine.GetMainLoop() as SceneTree)!.Paused = false;
        _root?.Free();
        _root = null;
        TestServices.Reinitialize();
        if (FileAccess.FileExists(_optionsPath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_optionsPath));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Options_button_opens_the_options_screen_and_its_Back_button_closes_it()
    {
        var menu = BuildMenu(new FakeSaveGame(true), out _, out _);
        var options = menu.GetNode<Control>("OptionsMenu");
        Assertions.AssertBool(options.Visible).IsFalse();

        menu.GetNode<Button>("Panel/OptionsButton").EmitSignal(BaseButton.SignalName.Pressed);
        Assertions.AssertBool(options.Visible).IsTrue();

        menu.GetNode<Button>("OptionsMenu/Layout/BackButton").EmitSignal(BaseButton.SignalName.Pressed);
        Assertions.AssertBool(options.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Quit_to_menu_with_a_saved_game_resumes_and_changes_to_the_main_menu()
    {
        var save = new FakeSaveGame(true);
        var menu = BuildMenu(save, out var scenes, out var pause);
        pause._UnhandledInput(EscapeEvent());
        Assertions.AssertBool(_root!.GetTree().Paused).IsTrue();

        menu.GetNode<Button>("Panel/QuitToMenuButton").EmitSignal(BaseButton.SignalName.Pressed);

        Assertions.AssertInt(save.Calls).IsEqual(1);
        Assertions.AssertBool(_root.GetTree().Paused).IsFalse();
        Assertions.AssertInt(scenes.Count).IsEqual(1);
        Assertions.AssertString(scenes[0]).IsEqual(MainMenuScenePath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Quit_to_menu_with_a_failed_save_stays_paused_and_does_not_change_the_scene()
    {
        var save = new FakeSaveGame(false);
        var menu = BuildMenu(save, out var scenes, out var pause);
        pause._UnhandledInput(EscapeEvent());

        menu.GetNode<Button>("Panel/QuitToMenuButton").EmitSignal(BaseButton.SignalName.Pressed);

        Assertions.AssertInt(save.Calls).IsEqual(1);
        Assertions.AssertBool(_root!.GetTree().Paused).IsTrue();
        Assertions.AssertInt(scenes.Count).IsEqual(0);
        Assertions.AssertString(menu.GetNode<Label>("Panel/SaveStatusLabel").Text).IsEqual("Save failed");
    }

    /// <summary>
    /// Builds a root holding a PauseController around a real PauseMenu.tscn instance. The options screen nested in the
    /// menu is injected by hand, as OptionsMenuSceneTest does, before the root enters the tree.
    /// </summary>
    private CanvasLayer BuildMenu(ISaveGameCommand save, out List<string> scenes, out PauseController pause)
    {
        TestServices.Container.RegisterSingleton<ISaveGameCommand>(save);
        var cursor = new CursorOwner();
        TestServices.Container.RegisterSingleton<ICursorOwner>(cursor);

        var menu = GD.Load<PackedScene>(PauseMenuScenePath).Instantiate<CanvasLayer>();
        TestServices.Inject(menu.GetNode<OptionsMenuController>("OptionsMenu"));

        var capturedScenes = new List<string>();
        scenes = capturedScenes;
        pause = new PauseController { PauseMenuLayer = menu, ChangeScene = path => capturedScenes.Add(path) };
        TestServices.Inject(pause);

        var root = new Node();
        root.AddChild(cursor);
        root.AddChild(menu);
        root.AddChild(pause);
        Assertions.AddNode(root, autoFree: false);
        _root = root;
        return menu;
    }

    private static InputEventAction EscapeEvent() => new() { Action = "ui_cancel", Pressed = true };

    private sealed class FakeSaveGame(bool result) : ISaveGameCommand
    {
        public int Calls { get; private set; }

        public bool ManualSave()
        {
            Calls++;
            return result;
        }
    }
}
