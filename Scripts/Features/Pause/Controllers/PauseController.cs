using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Pause.Controllers;

public partial class PauseController : Node, IDependent
{
    private Control _pauseMenuRoot = null!;
    private ICursorOwner? _cursor;
    private ISaveGameCommand? _saveGame;
    private Label? _saveStatusLabel;
    private Control? _optionsMenu;
    private IServiceResolver? _services;

    private const string MainMenuScenePath = "res://Scenes/UI/MainMenu.tscn";

    [Export] public CanvasLayer PauseMenuLayer = null!;
    [Export] public CanvasLayer? UILayer;

    /// <summary>
    ///     Changes the scene when the player quits to the main menu. Set it before the node enters the tree to replace the
    ///     default; _Ready keeps a value that is already set and otherwise uses the tree's ChangeSceneToFile.
    /// </summary>
    public Action<string>? ChangeScene;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        // Always run so we can unpause via input
        ProcessMode = ProcessModeEnum.Always;
        ChangeScene ??= path => GetTree().ChangeSceneToFile(path);

        // Cache and hide the pause menu
        _pauseMenuRoot = PauseMenuLayer.GetNode<Control>("Panel");
        _pauseMenuRoot.Visible = false;
        _pauseMenuRoot.SetProcessMode(ProcessModeEnum.WhenPaused);
        PauseMenuLayer.GetNode<Button>("Panel/ResumeButton").Pressed += Resume;

        // The Save button and its status line are optional: a menu without them still pauses and resumes
        _saveStatusLabel = PauseMenuLayer.GetNodeOrNull<Label>("Panel/SaveStatusLabel");
        if (PauseMenuLayer.GetNodeOrNull<Button>("Panel/SaveButton") is { } saveButton)
            saveButton.Pressed += OnSavePressed;

        // The options screen and Quit to menu are optional too; a menu without them still pauses and resumes.
        // The options screen is a child of the menu, hidden until Options opens it and closed by its Back button.
        _optionsMenu = PauseMenuLayer.GetNodeOrNull<Control>("OptionsMenu");
        if (_optionsMenu != null)
        {
            _optionsMenu.SetProcessMode(ProcessModeEnum.WhenPaused);
            _optionsMenu.Visible = false;
            _optionsMenu.Connect("Closed", Callable.From(() => CloseOptions()));
            if (PauseMenuLayer.GetNodeOrNull<Button>("Panel/OptionsButton") is { } optionsButton)
                optionsButton.Pressed += OpenOptions;
        }

        if (PauseMenuLayer.GetNodeOrNull<Button>("Panel/QuitToMenuButton") is { } quitButton)
            quitButton.Pressed += OnQuitToMenuPressed;

        // In-game UI starts visible
        if (UILayer != null)
            UILayer.Visible = true;

        if (_services == null)
        {
            ILog.Error("PauseController was not injected");
            return;
        }

        _services.Get<ICursorOwner>(cursor => _cursor = cursor);
        _services.Get<ISaveGameCommand>(saveGame => _saveGame = saveGame);
    }

    private void OnSavePressed()
    {
        // No save command (for example, no save service in this scene) reports as a failed save
        var saved = _saveGame?.ManualSave() ?? false;
        if (_saveStatusLabel != null)
            _saveStatusLabel.Text = saved ? "Saved" : "Save failed";
    }

    private void OpenOptions()
    {
        if (_optionsMenu != null)
            _optionsMenu.Visible = true;
    }

    private void CloseOptions()
    {
        if (_optionsMenu != null)
            _optionsMenu.Visible = false;
    }

    private void OnQuitToMenuPressed()
    {
        // Leave only after the save went through: a failed save keeps the player here, paused, with the message shown
        var saved = _saveGame?.ManualSave() ?? false;
        if (_saveStatusLabel != null)
            _saveStatusLabel.Text = saved ? "Saved" : "Save failed";
        if (!saved)
            return;

        Resume();
        ChangeScene!(MainMenuScenePath);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel"))
            return;

        if (GetTree().Paused)
        {
            Resume();
            return;
        }

        // While something else holds the cursor (a PC screen), Escape belongs
        // to that screen, not to pause
        if (_cursor != null && _cursor.HasClaimOtherThan(this))
            return;

        Pause();
    }

    private void Pause()
    {
        GetTree().Paused = true;
        _pauseMenuRoot.Visible = true;
        if (UILayer != null)
            UILayer.Visible = false;

        // The pause menu holds a cursor claim while it is open
        _cursor?.Claim(this);
    }

    private void Resume()
    {
        GetTree().Paused = false;
        _pauseMenuRoot.Visible = false;
        if (_optionsMenu != null)
            _optionsMenu.Visible = false;
        if (UILayer != null)
            UILayer.Visible = true;

        _cursor?.Release(this);
    }
}
