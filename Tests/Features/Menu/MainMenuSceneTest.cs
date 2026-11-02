using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Features.Menu.Controllers;
using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

/// <summary>
/// The main menu instanced from MainMenu.tscn, driven through the buttons' pressed signals. The click path through
/// real input events is MainMenuClickTest. Fixture and teardown live in MainMenuSceneTestBase.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuSceneTest : MainMenuSceneTestBase
{
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

        SubmitNewGameName("My Run");

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
            SubmitNewGameName(raw);

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
        SeedUnreadableSlot("broken");
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

    // Opens the name dialog through the New game button, types the name and confirms it.
    private void SubmitNewGameName(string raw)
    {
        Press(Get<Button>("Layout/NewGameButton"));
        Get<LineEdit>("NewGameDialog/NameEdit").Text = raw;
        Get<ConfirmationDialog>("NewGameDialog").EmitSignal("confirmed");
    }
}
