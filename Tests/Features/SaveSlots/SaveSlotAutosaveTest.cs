using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
/// The autosave timer writes the active slot and nothing else. The service runs against an in-memory slot file
/// system, and the real user://game_save.json must not be written or created by it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SaveSlotAutosaveTest
{
    private const float ShortInterval = 0.05f;

    [TestCase]
    [TestCategory("Unit")]
    public static async Task AutosaveWritesOnlyTheActiveSlot()
    {
        using var scene = new SaveWorld("SlotAutosaveWorld");
        var files = new InMemorySlotFileSystem();
        var service = scene.Add(new GameSaveService(files, SaveWorld.SlotNamed("a"))
        {
            AutoLoadOnStart = false,
            AutoSaveInterval = ShortInterval,
        });
        AddNode(scene.World, autoFree: false);

        // ASSUMPTION: a real user:// file is only checked by its modified time, which stays 0 when it is absent; the
        // service talks to the in-memory file system, so this check guards against a regression that bypasses it.
        var legacyModifiedBefore = FileAccess.GetModifiedTime(LegacySaveImporter.LegacyPath);
        scene.Enter();

        // The timer's Timeout handler (SaveGame) was connected in _Ready, before this await, so it runs first.
        var timer = service.GetChildren().OfType<Timer>().Single();
        await service.ToSignal(timer, Timer.SignalName.Timeout);

        AssertBool(files.Exists(SaveSlotLayout.FileFor(SlotName("a")))).IsTrue();
        AssertThat(new SlotStore(files).List().Count).IsEqual(1);
        AssertBool(files.Exists(SaveSlotLayout.FileFor(SlotName("b")))).IsFalse();
        AssertBool(files.Exists(LegacySaveImporter.LegacyPath)).IsFalse();
        AssertThat(FileAccess.GetModifiedTime(LegacySaveImporter.LegacyPath)).IsEqual(legacyModifiedBefore);
    }

    private static SaveSlotName SlotName(string raw)
    {
        if (!SaveSlotName.TryCreate(raw, out var name, out var reason))
            throw new InvalidOperationException(reason);

        return name;
    }
}
