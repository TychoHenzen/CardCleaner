using System;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Menu.Controllers;

/// <summary>
///     The main menu: continue, new game, load, delete, options and quit. It applies the engine-global options, imports
///     an older single-file save once before it lists the slots, and then lists them. The saved mouse options are applied
///     when the shop starts (MouseOptionsStartup), not here. Every failure is logged and shown in the message label.
/// </summary>
public partial class MainMenuController : Control, IDependent
{
    public const string ShopScenePath = "res://Scenes/Gameplay/ShopScene.tscn";

    // The paths name the controls inside MainMenu.tscn.
    private const string ContinuePath = "Layout/ContinueButton";
    private const string NewGamePath = "Layout/NewGameButton";
    private const string SlotListPath = "Layout/SlotList";
    private const string OptionsButtonPath = "Layout/OptionsButton";
    private const string QuitPath = "Layout/QuitButton";
    private const string MessagePath = "Layout/MessageLabel";
    private const string NewGameDialogPath = "NewGameDialog";
    private const string NameEditPath = "NewGameDialog/NameEdit";
    private const string DeleteDialogPath = "DeleteDialog";
    private const string OptionsPath = "Options";

    /// <summary>
    ///     Changes the scene. Set it before the node enters the tree to replace the default; _Ready keeps a value that is
    ///     already set and otherwise uses the tree's ChangeSceneToFile.
    /// </summary>
    public Action<string>? ChangeScene;

    private IServiceResolver? _services;
    private ISaveSlotCatalog _catalog = null!;
    private IActiveSaveSlot _activeSlot = null!;
    private ILegacySaveImporter _importer = null!;
    private OptionsStore _store = null!;
    private OptionsApplier _applier = null!;
    private SaveSlotInfo? _pendingDelete;

    public void Inject(IServiceResolver services)
    {
        _services = services;
    }

    public override void _Ready()
    {
        _catalog = _services!.Get<ISaveSlotCatalog>();
        _activeSlot = _services.Get<IActiveSaveSlot>();
        _importer = _services.Get<ILegacySaveImporter>();
        _store = _services.Get<OptionsStore>();
        _applier = _services.Get<OptionsApplier>();
        ChangeScene ??= path => GetTree().ChangeSceneToFile(path);

        GetNode<Button>(ContinuePath).Pressed += OnContinuePressed;
        GetNode<Button>(NewGamePath).Pressed += OnNewGamePressed;
        GetNode<Button>(OptionsButtonPath).Pressed += () => GetNode<Control>(OptionsPath).Visible = true;
        GetNode<OptionsMenuController>(OptionsPath).Closed += () => GetNode<Control>(OptionsPath).Visible = false;
        GetNode<Button>(QuitPath).Pressed += () => GetTree().Quit();
        GetNode<SlotListController>(SlotListPath).LoadRequested += OnLoadRequested;
        GetNode<SlotListController>(SlotListPath).DeleteRequested += OnDeleteRequested;
        GetNode<ConfirmationDialog>(NewGameDialogPath).Confirmed += OnNewGameConfirmed;
        GetNode<ConfirmationDialog>(DeleteDialogPath).Confirmed += OnDeleteConfirmed;
        GetNode<ConfirmationDialog>(DeleteDialogPath).Canceled += () => _pendingDelete = null;

        // Engine-global options only; the mouse options are applied when the shop starts (MouseOptionsStartup).
        ReportOnFailure("Could not apply the saved options.", () => _applier.Apply(_store.Load(), GetWindow()));
        Input.MouseMode = Input.MouseModeEnum.Visible;
        // The import runs before the first listing, so an older game_save.json is in the "imported" slot when it shows.
        ReportOnFailure("Could not import the older save.", () => _importer.TryImport());
        Refresh();
    }

    private void OnContinuePressed()
    {
        ReportOnFailure("Could not continue.", () =>
        {
            var entry = _catalog.List().FirstOrDefault(info => info.IsReadable);
            if (entry is not null)
                StartGame(entry.Name);
        });
    }

    private void OnNewGamePressed()
    {
        GetNode<LineEdit>(NameEditPath).Text = string.Empty;
        GetNode<Label>(MessagePath).Text = string.Empty;
        GetNode<ConfirmationDialog>(NewGameDialogPath).PopupCentered();
    }

    private void OnNewGameConfirmed()
    {
        ReportOnFailure("Could not create the save slot.", () =>
        {
            if (!_catalog.TryCreate(GetNode<LineEdit>(NameEditPath).Text, out var name, out var reason))
            {
                GetNode<Label>(MessagePath).Text = reason;
                return;
            }

            StartGame(name);
        });
    }

    private void OnLoadRequested(SaveSlotInfo info)
    {
        if (info.IsReadable)
            StartGame(info.Name);
    }

    private void OnDeleteRequested(SaveSlotInfo info)
    {
        _pendingDelete = info;
        var dialog = GetNode<ConfirmationDialog>(DeleteDialogPath);
        dialog.DialogText = $"Delete the save slot \"{info.Name.Value}\"? This cannot be undone.";
        dialog.PopupCentered();
    }

    private void OnDeleteConfirmed()
    {
        var pending = _pendingDelete;
        _pendingDelete = null;
        if (pending is null)
            return;

        ReportOnFailure("Could not delete the save slot.", () => _catalog.Delete(pending.Name));
        Refresh();
    }

    private void StartGame(SaveSlotName name)
    {
        ReportOnFailure("Could not open the save slot.", () =>
        {
            _activeSlot.Activate(name);
            ChangeScene!(ShopScenePath);
        });
    }

    private void Refresh()
    {
        ReportOnFailure("Could not list the save slots.", () =>
        {
            var slots = _catalog.List();
            GetNode<Button>(ContinuePath).Disabled = !slots.Any(info => info.IsReadable);
            GetNode<SlotListController>(SlotListPath).Show(slots);
        });
    }

    private void ReportOnFailure(string message, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ILog.Error($"{message} {ex}");
            GetNode<Label>(MessagePath).Text = message;
        }
    }
}
