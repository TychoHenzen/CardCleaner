using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Controllers;
using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

/// <summary>
/// The main menu instanced from MainMenu.tscn. Slots live in an in-memory file system and the options in a throw-away
/// user:// file. ChangeScene is replaced with a recorder. The teardown restores the audio buses and the window size.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuSceneTest
{
    private const string ScenePath = "res://Scenes/UI/MainMenu.tscn";
    private static readonly DateTime OlderSave = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NewerSave = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private InMemorySlotFileSystem _files = null!;
    private SlotStore _store = null!;
    private ActiveSaveSlot _active = null!;
    private FakeImporter _importer = null!;
    private string _path = null!;
    private Window _window = null!;
    private Vector2I _originalWindowSize;
    private MainMenuController? _node;
    private List<string> _changedScenes = null!;

    [BeforeTest]
    public void Setup()
    {
        // Reinitialize clears the container and composes the autoload again; the registrations below replace it.
        TestServices.Reinitialize();
        _files = new InMemorySlotFileSystem();
        _store = new SlotStore(_files);
        _active = new ActiveSaveSlot();
        _importer = new FakeImporter();
        _path = $"user://test_main_menu_{Guid.NewGuid():N}.cfg";
        _changedScenes = new List<string>();

        TestServices.Container.RegisterSingleton<ISaveSlotCatalog>(_store);
        TestServices.Container.RegisterSingleton<IActiveSaveSlot>(_active);
        TestServices.Container.RegisterSingleton<ILegacySaveImporter>(_importer);
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
        // Restore the buses through a sink that does not touch the window.
        new OptionsApplier(static (_, _) => { }).Apply(GameOptions.Defaults, _window);
        _window.Size = _originalWindowSize;
        TestServices.Reinitialize();
        if (FileAccess.FileExists(_path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_path));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Continue_disabled_with_no_slots()
    {
        AddMenu();

        Assertions.AssertBool(Get<Button>("Layout/ContinueButton").Disabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void New_game_with_valid_name_creates_slot_activates_it_and_changes_to_ShopScenePath()
    {
        AddMenu();

        Press(Get<Button>("Layout/NewGameButton"));
        Get<LineEdit>("NewGameDialog/NameEdit").Text = "My Run";
        Get<ConfirmationDialog>("NewGameDialog").EmitSignal("confirmed");

        Assertions.AssertInt(_store.List().Count).IsEqual(1);
        Assertions.AssertString(_active.Current.Value).IsEqual("My Run");
        Assertions.AssertInt(_changedScenes.Count).IsEqual(1);
        Assertions.AssertString(_changedScenes[0]).IsEqual(MainMenuController.ShopScenePath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void New_game_refuses_empty_duplicate_and_path_name()
    {
        SaveSlot("alpha", OlderSave);
        AddMenu();
        var before = _store.List().Count;

        foreach (var raw in new[] { string.Empty, "ALPHA", "bad/name" })
        {
            Press(Get<Button>("Layout/NewGameButton"));
            Get<LineEdit>("NewGameDialog/NameEdit").Text = raw;
            Get<ConfirmationDialog>("NewGameDialog").EmitSignal("confirmed");

            Assertions.AssertBool(string.IsNullOrEmpty(Get<Label>("Layout/MessageLabel").Text)).IsFalse();
        }

        Assertions.AssertInt(_store.List().Count).IsEqual(before);
        Assertions.AssertInt(_changedScenes.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Continue_picks_newest_of_two()
    {
        SaveSlot("older", OlderSave);
        SaveSlot("newer", NewerSave);
        AddMenu();

        Assertions.AssertBool(Get<Button>("Layout/ContinueButton").Disabled).IsFalse();
        Press(Get<Button>("Layout/ContinueButton"));

        Assertions.AssertString(_active.Current.Value).IsEqual("newer");
        Assertions.AssertInt(_changedScenes.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_button_activates_that_slot()
    {
        SaveSlot("older", OlderSave);
        SaveSlot("newer", NewerSave);
        AddMenu();

        Press(Row("older").GetNode<Button>("Load"));

        Assertions.AssertString(_active.Current.Value).IsEqual("older");
        Assertions.AssertInt(_changedScenes.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Delete_cancel_keeps_slot_confirm_removes_only_it()
    {
        SaveSlot("alpha", OlderSave);
        SaveSlot("beta", NewerSave);
        AddMenu();

        Press(Row("alpha").GetNode<Button>("Delete"));
        Get<ConfirmationDialog>("DeleteDialog").EmitSignal("canceled");
        Assertions.AssertInt(_store.List().Count).IsEqual(2);

        Press(Row("alpha").GetNode<Button>("Delete"));
        Get<ConfirmationDialog>("DeleteDialog").EmitSignal("confirmed");

        var names = _store.List().Select(info => info.Name.Value).ToList();
        Assertions.AssertInt(names.Count).IsEqual(1);
        Assertions.AssertString(names[0]).IsEqual("beta");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Unreadable_slot_listed_with_tag_and_deletable_but_Load_disabled()
    {
        _store.TryCreate("broken", out _, out _);
        _files.Seed(SaveSlotLayout.FileFor(Name("broken")), "not a save");
        AddMenu();

        var row = Row("broken");
        Assertions.AssertBool(row.GetNode<Label>("Label").Text.Contains("Unreadable")).IsTrue();
        Assertions.AssertBool(row.GetNode<Button>("Load").Disabled).IsTrue();
        Assertions.AssertBool(row.GetNode<Button>("Delete").Disabled).IsFalse();
        Assertions.AssertBool(Get<Button>("Layout/ContinueButton").Disabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Importer_runs_once_before_listing()
    {
        _importer.Import = () => _store.Save(Name("imported"), "legacy", OlderSave);

        AddMenu();

        Assertions.AssertInt(_importer.Calls).IsEqual(1);
        Assertions.AssertBool(Get<Button>("Layout/ContinueButton").Disabled).IsFalse();
        Assertions.AssertString(Row("imported").GetNode<Label>("Label").Text.Split("  ")[0]).IsEqual("imported");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Menu_applies_stored_audio_options_without_a_GameSettings()
    {
        new OptionsStore(_path).Save(GameOptions.Defaults with { MasterVolume = 0.5f });

        AddMenu();

        var masterDb = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"));
        Assertions.AssertFloat(masterDb).IsEqualApprox(-6.02f, 0.05f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Menu_sends_stored_display_options_to_the_display_sink()
    {
        var options = GameOptions.Defaults with { Fullscreen = true, Vsync = false };
        new OptionsStore(_path).Save(options);
        var plans = new List<DisplayPlan>();
        TestServices.Container.RegisterSingleton<OptionsApplier>(new OptionsApplier((plan, _) => plans.Add(plan)));

        AddMenu();

        Assertions.AssertInt(plans.Count).IsEqual(1);
        Assertions.AssertThat(plans[0]).IsEqual(OptionsApplier.DisplayFor(options));
        Assertions.AssertBool(plans[0].Mode == DisplayServer.WindowMode.ExclusiveFullscreen).IsTrue();
        Assertions.AssertBool(plans[0].Size.HasValue).IsFalse();
        Assertions.AssertBool(plans[0].VSync == DisplayServer.VSyncMode.Disabled).IsTrue();
    }

    // Clicks go through Input.ParseInputEvent at the centre of the button's global rect, so they reach the
    // GUI input path the player uses. No test in this class calls EmitSignal in place of a click; the dialog
    // buttons inside ConfirmationDialog are still driven through their confirmed/canceled signals.
    [TestCase]
    [TestCategory("Unit")]
    public async Task Load_button_click_activates_that_slot()
    {
        SaveSlot("older", OlderSave);
        SaveSlot("newer", NewerSave);
        AddMenu();
        await Settle();

        await Click(Row("older").GetNode<Button>("Load"));

        Assertions.AssertString(_active.Current.Value).IsEqual("older");
        Assertions.AssertInt(_changedScenes.Count).IsEqual(1);
        Assertions.AssertString(_changedScenes[0]).IsEqual(MainMenuController.ShopScenePath);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Load_click_on_an_unreadable_slot_does_nothing()
    {
        _store.TryCreate("broken", out _, out _);
        _files.Seed(SaveSlotLayout.FileFor(Name("broken")), "not a save");
        AddMenu();
        await Settle();

        await Click(Row("broken").GetNode<Button>("Load"));

        Assertions.AssertInt(_changedScenes.Count).IsEqual(0);
        Assertions.AssertString(_active.Current.Value).IsEqual("default");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Delete_button_click_opens_the_confirmation_and_confirm_removes_only_that_slot()
    {
        SaveSlot("alpha", OlderSave);
        SaveSlot("beta", NewerSave);
        AddMenu();
        await Settle();

        await Click(Row("alpha").GetNode<Button>("Delete"));

        var dialog = Get<ConfirmationDialog>("DeleteDialog");
        Assertions.AssertBool(dialog.Visible).IsTrue();
        Assertions.AssertBool(dialog.DialogText.Contains("alpha", StringComparison.Ordinal)).IsTrue();

        dialog.EmitSignal("confirmed");
        await CloseDialog("DeleteDialog");
        await Settle();

        var names = _store.List().Select(info => info.Name.Value).ToList();
        Assertions.AssertInt(names.Count).IsEqual(1);
        Assertions.AssertString(names[0]).IsEqual("beta");
        Assertions.AssertInt(RowCount()).IsEqual(1);
        Assertions.AssertBool(Row("beta") is not null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Delete_click_then_cancel_keeps_the_slot()
    {
        SaveSlot("alpha", OlderSave);
        AddMenu();
        await Settle();

        await Click(Row("alpha").GetNode<Button>("Delete"));
        Assertions.AssertBool(Get<ConfirmationDialog>("DeleteDialog").Visible).IsTrue();

        Get<ConfirmationDialog>("DeleteDialog").EmitSignal("canceled");
        await CloseDialog("DeleteDialog");
        await Settle();

        Assertions.AssertInt(_store.List().Count).IsEqual(1);
        Assertions.AssertInt(RowCount()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Continue_button_click_loads_the_newest_slot()
    {
        SaveSlot("older", OlderSave);
        SaveSlot("newer", NewerSave);
        AddMenu();
        await Settle();

        await Click(Get<Button>("Layout/ContinueButton"));

        Assertions.AssertString(_active.Current.Value).IsEqual("newer");
        Assertions.AssertInt(_changedScenes.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task New_game_button_click_opens_the_name_dialog()
    {
        AddMenu();
        await Settle();

        await Click(Get<Button>("Layout/NewGameButton"));

        Assertions.AssertBool(Get<ConfirmationDialog>("NewGameDialog").Visible).IsTrue();
        Assertions.AssertString(Get<LineEdit>("NewGameDialog/NameEdit").Text).IsEmpty();
        await CloseDialog("NewGameDialog");
        Assertions.AssertInt(_changedScenes.Count).IsEqual(0);
    }

    private async Task Settle()
    {
        await Frame();
        await Frame();
    }

    private async Task CloseDialog(string path)
    {
        Get<ConfirmationDialog>(path).Hide();
        await Frame();
    }

    private static async Task Frame() => await ISceneRunner.SyncProcessFrame;

    private static void Send(InputEvent input)
    {
        Input.ParseInputEvent(input);
        Input.FlushBufferedEvents();
    }

    private static async Task Click(Control control)
    {
        var rect = control.GetGlobalRect();
        Assertions.AssertBool(rect.Size.X > 0 && rect.Size.Y > 0).IsTrue();
        Assertions.AssertBool(control.IsVisibleInTree()).IsTrue();
        var point = rect.GetCenter();

        Send(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        await Frame();
        Send(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            ButtonMask = MouseButtonMask.Left,
            Position = point,
            GlobalPosition = point,
        });
        await Frame();
        Send(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = false,
            ButtonMask = 0,
            Position = point,
            GlobalPosition = point,
        });
        await Frame();
    }

    private int RowCount()
    {
        return Get<VBoxContainer>("Layout/SlotList").GetChildren().OfType<HBoxContainer>().Count();
    }

    private T Get<T>(string path) where T : Node => _node!.GetNode<T>(path);

    private HBoxContainer Row(string slotName)
    {
        foreach (var child in Get<VBoxContainer>("Layout/SlotList").GetChildren())
        {
            if (child is HBoxContainer row
                && row.GetNode<Label>("Label").Text.StartsWith(slotName + "  ", StringComparison.Ordinal))
                return row;
        }

        throw new InvalidOperationException($"No slot row for '{slotName}'.");
    }

    private static void Press(BaseButton button) => button.EmitSignal(BaseButton.SignalName.Pressed);

    private static SaveSlotName Name(string raw)
    {
        if (!SaveSlotName.TryCreate(raw, out var name, out var reason))
            throw new InvalidOperationException(reason);
        return name;
    }

    private void SaveSlot(string raw, DateTime savedAtUtc)
    {
        _store.Save(Name(raw), "payload", savedAtUtc);
    }

    private void AddMenu()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        _node = (MainMenuController)packed.Instantiate();
        _node.ChangeScene = path => _changedScenes.Add(path);
        TestServices.Inject(_node);
        Assertions.AddNode(_node, autoFree: false);
    }

    private sealed class FakeImporter : ILegacySaveImporter
    {
        public int Calls;

        public Action? Import;

        public bool TryImport()
        {
            Calls++;
            Import?.Invoke();
            return Import is not null;
        }
    }
}
