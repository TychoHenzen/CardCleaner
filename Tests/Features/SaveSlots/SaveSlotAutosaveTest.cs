using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
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
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousScene = tree.CurrentScene;
        TestServices.Reset();

        var files = new InMemorySlotFileSystem();
        var slots = new ActiveSaveSlot();
        if (!SaveSlotName.TryCreate("a", out var slotA, out var reason))
            throw new InvalidOperationException(reason);
        slots.Activate(slotA);

        var world = new Node3D { Name = "SlotAutosaveWorld" };
        var service = TestServices.Inject(new GameSaveService(files, slots)
        {
            AutoLoadOnStart = false,
            AutoSaveInterval = ShortInterval,
        });
        world.AddChild(service);
        AddNode(world, autoFree: false);

        // ASSUMPTION: a real user:// file is only checked by its modified time, which stays 0 when it is absent; the
        // service talks to the in-memory file system, so this check guards against a regression that bypasses it.
        var legacyModifiedBefore = FileAccess.GetModifiedTime(LegacySaveImporter.LegacyPath);

        try
        {
            tree.CurrentScene = world;

            // The timer's Timeout handler (SaveGame) was connected in _Ready, before this await, so it runs first.
            var timer = service.GetChildren().OfType<Timer>().Single();
            await service.ToSignal(timer, Timer.SignalName.Timeout);

            AssertBool(files.Exists(SaveSlotLayout.FileFor(slotA))).IsTrue();
            AssertThat(new SlotStore(files).List().Count).IsEqual(1);

            if (!SaveSlotName.TryCreate("b", out var slotB, out _))
                throw new InvalidOperationException("slot name b must be valid");
            AssertBool(files.Exists(SaveSlotLayout.FileFor(slotB))).IsFalse();

            AssertBool(files.Exists(LegacySaveImporter.LegacyPath)).IsFalse();
            AssertThat(FileAccess.GetModifiedTime(LegacySaveImporter.LegacyPath)).IsEqual(legacyModifiedBefore);
        }
        finally
        {
            TearDown(tree, previousScene, world, service);
        }
    }

    private static void TearDown(SceneTree tree, Node? previousScene, Node3D world, GameSaveService service)
    {
        world.RemoveChild(service);
        service.Free();
        world.Free();
        tree.CurrentScene = previousScene;
        TestServices.Reset();
    }
}
