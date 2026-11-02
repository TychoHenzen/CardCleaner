using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

/// <summary>
/// Options never touch the save slots. The snapshot of user://saves is read-only: the test never creates, writes or
/// deletes anything under it. The options file lives at a throw-away user:// path that the teardown deletes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OptionsSlotIsolationTest
{
    private static readonly string[] OptionsSources =
    {
        "res://Scripts/Features/Menu/Services/OptionsApplier.cs",
        "res://Scripts/Features/Menu/Services/OptionsStore.cs",
        "res://Scripts/Features/Menu/Services/DisplayPlan.cs",
        "res://Scripts/Features/Menu/Models/GameOptions.cs",
    };

    private static readonly string[] ForbiddenSaveNames = { "SaveSlots", "user://saves", "SaveSlotLayout" };

    private string _path = null!;
    private Window _window = null!;

    [BeforeTest]
    public void Setup()
    {
        _path = $"user://test_options_isolation_{Guid.NewGuid():N}.cfg";
        _window = new Window();
    }

    [AfterTest]
    public void Teardown()
    {
        if (FileAccess.FileExists(_path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_path));

        // Restore the buses through a sink that does not touch the window, then free the window.
        new OptionsApplier(static (_, _) => { }).Apply(GameOptions.Defaults, _window);
        _window.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Applying_and_saving_options_leaves_user_saves_untouched()
    {
        var before = SlotSnapshot();
        var savesFolderExisted = DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(SaveSlotLayout.Root));

        var options = GameOptions.Defaults with { Fullscreen = true, MasterVolume = 0.5f, MouseSensitivity = 0.3f };
        var store = new OptionsStore(_path);
        store.Save(options);
        var loaded = store.Load();

        var plans = new List<DisplayPlan>();
        var applier = new OptionsApplier((plan, _) => plans.Add(plan));
        var settings = new GameSettings();
        try
        {
            applier.Apply(loaded, _window);
            applier.ApplyMouse(loaded, settings);
        }
        finally
        {
            settings.Free();
        }

        // Sanity check that the apply ran, so the snapshot comparison below is not vacuous.
        Assertions.AssertThat(plans.Count).IsEqual(1);
        Assertions.AssertThat(SlotSnapshot()).IsEqual(before);
        Assertions.AssertThat(DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(SaveSlotLayout.Root)))
            .IsEqual(savesFolderExisted);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Options_sources_do_not_name_the_save_slot_layer()
    {
        var violations = new List<string>();
        foreach (var source in OptionsSources)
        {
            var text = System.IO.File.ReadAllText(ProjectSettings.GlobalizePath(source));
            foreach (var forbidden in ForbiddenSaveNames)
            {
                if (text.Contains(forbidden, StringComparison.Ordinal))
                    violations.Add($"{source} names {forbidden}");
            }
        }

        Assertions.AssertThat(string.Join("\n", violations)).IsEmpty();
    }

    // Read-only listing of user://saves: each slot folder's files with their modified time and size.
    private static List<string> SlotSnapshot()
    {
        var entries = new List<string>();
        using var root = DirAccess.Open(SaveSlotLayout.Root);
        if (root == null)
            return entries;

        foreach (var folder in root.GetDirectories())
        {
            var folderPath = $"{SaveSlotLayout.Root}/{folder}";
            using var dir = DirAccess.Open(folderPath);
            if (dir == null)
                continue;

            foreach (var file in dir.GetFiles())
            {
                var filePath = $"{folderPath}/{file}";
                entries.Add($"{folder}/{file}|{FileAccess.GetModifiedTime(filePath)}|{SizeOf(filePath)}");
            }
        }

        entries.Sort(StringComparer.Ordinal);
        return entries;
    }

    private static ulong SizeOf(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file == null ? 0UL : file.GetLength();
    }
}
