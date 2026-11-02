using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Menu.Controllers;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

/// <summary>
/// The main menu instanced from MainMenu.tscn, driven with real mouse input. Clicks go through
/// Input.ParseInputEvent at the centre of the button's global rect, so they reach the GUI input path the player
/// uses. The dialog buttons inside ConfirmationDialog are still driven through their confirmed/canceled signals.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuClickTest : MainMenuSceneTestBase
{
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
        SeedUnreadableSlot("broken");
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
        AssertDeleteDialogNames("alpha");
        await ConfirmDelete();

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

    [TestCase]
    [TestCategory("Unit")]
    public async Task Unreadable_slot_delete_through_the_menu_removes_it_and_a_new_game_can_reuse_the_name()
    {
        _store.TryCreate("broken", out var broken, out _);
        _files.Seed(SaveSlotLayout.FileFor(broken), "not a save");
        _files.Seed(SaveSlotLayout.TempFor(broken), "half written");
        AddMenu();
        await Settle();

        var row = Row("broken");
        var tag = row.GetNode<Label>("Label").Text;
        Assertions.AssertBool(tag.Contains("Unreadable", StringComparison.Ordinal)).IsTrue();
        Assertions.AssertBool(Get<Button>("Layout/ContinueButton").Disabled).IsTrue();

        await Click(row.GetNode<Button>("Delete"));
        AssertDeleteDialogNames("broken");
        await ConfirmDelete();

        Assertions.AssertInt(_store.List().Count).IsEqual(0);
        Assertions.AssertBool(_files.Exists(SaveSlotLayout.FileFor(broken))).IsFalse();
        Assertions.AssertBool(_files.Exists(SaveSlotLayout.TempFor(broken))).IsFalse();
        Assertions.AssertBool(_files.ListFolders(SaveSlotLayout.Root).Contains(broken.Folder)).IsFalse();
        Assertions.AssertInt(RowCount()).IsEqual(0);
        Assertions.AssertBool(Get<Button>("Layout/ContinueButton").Disabled).IsTrue();
        Assertions.AssertString(Get<Label>("Layout/MessageLabel").Text).IsEmpty();

        await Click(Get<Button>("Layout/NewGameButton"));
        Get<LineEdit>("NewGameDialog/NameEdit").Text = "broken";
        Get<ConfirmationDialog>("NewGameDialog").EmitSignal("confirmed");
        await CloseDialog("NewGameDialog");
        await Settle();

        Assertions.AssertInt(_changedScenes.Count).IsEqual(1);
        Assertions.AssertString(_changedScenes[0]).IsEqual(MainMenuController.ShopScenePath);
        Assertions.AssertString(_active.Current.Value).IsEqual("broken");
        Assertions.AssertInt(_store.List().Count).IsEqual(1);
    }

    private static async Task Settle()
    {
        await Frame();
        await Frame();
    }

    private static async Task Frame() => await ISceneRunner.SyncProcessFrame;

    private static void Send(InputEvent input)
    {
        Input.ParseInputEvent(input);
        Input.FlushBufferedEvents();
    }

    private async Task CloseDialog(string path)
    {
        Get<ConfirmationDialog>(path).Hide();
        await Frame();
    }

    // The delete dialog is open and visible and names the slot it would remove.
    private void AssertDeleteDialogNames(string slotName)
    {
        var dialog = Get<ConfirmationDialog>("DeleteDialog");
        Assertions.AssertBool(dialog.Visible).IsTrue();
        Assertions.AssertBool(dialog.DialogText.Contains(slotName, StringComparison.Ordinal)).IsTrue();
    }

    private async Task ConfirmDelete()
    {
        Get<ConfirmationDialog>("DeleteDialog").EmitSignal("confirmed");
        await CloseDialog("DeleteDialog");
        await Settle();
    }

    private static async Task Click(Control control)
    {
        var rect = control.GetGlobalRect();
        Assertions.AssertBool(rect.Size.X > 0 && rect.Size.Y > 0).IsTrue();
        Assertions.AssertBool(control.IsVisibleInTree()).IsTrue();
        var point = rect.GetCenter();

        Send(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        await Frame();
        await SendAndWait(ButtonEvent(point, pressed: true));
        await SendAndWait(ButtonEvent(point, pressed: false));
    }

    private static InputEventMouseButton ButtonEvent(Vector2 point, bool pressed)
    {
        return new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = pressed,
            ButtonMask = pressed ? MouseButtonMask.Left : 0,
            Position = point,
            GlobalPosition = point,
        };
    }

    // Sends the event, then waits one frame for the GUI to handle it.
    private static async Task SendAndWait(InputEvent input)
    {
        Send(input);
        await Frame();
    }
}
