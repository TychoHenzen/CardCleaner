using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.Features.Packs.Components;
using CardCleaner.Tests.Features.SaveSlots.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
/// Saves a sealed cardboard box, reloads it from the slot, then opens the original and the reloaded box all the way
/// down to their booster cards. Both must spawn the same card signatures. The recording spawner keeps the cards out
/// of the tree, and the in-memory slot file system keeps the test off the user folder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SealedBoxSaveTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const ulong BoxSeed = 0x5EA1EDUL;

    [TestCase]
    public static async Task ReloadedBoxOpensIntoTheSameCardSignaturesAsTheOriginal()
    {
        using var scene = new SaveWorld("SealedBoxWorld", addCardsToTree: false);
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.ContentSeed = BoxSeed;
        box.Spawning = scene.Spawner;
        scene.World.AddChild(box);
        var service = scene.Add(
            new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("a")) { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        service.SaveGame();

        // The original box opens at once; the wait opens every container the opening brings in.
        await OpenUntilEmpty(scene);
        var original = Canonical(scene.Spawner);
        AssertThat(original.Count).IsEqual(CardContainerLayout.CardsPerBox);

        scene.Spawner.Spawned.Clear();
        service.LoadGame();
        await scene.TwoFrames();

        var restored = ContainersIn(scene.Tree).Where(c => c.Kind == CardContainerKind.Box).ToList();
        AssertThat(restored).HasSize(1);
        var reloadedBox = restored.Single();
        AssertThat(reloadedBox.ContentSeed).IsEqual(BoxSeed);

        // A restored container has no spawner of its own: the test hands it the recording one, as the shop does.
        reloadedBox.Spawning = scene.Spawner;
        await OpenUntilEmpty(scene);
        var reopened = Canonical(scene.Spawner);
        AssertThat(reopened.Count).IsEqual(CardContainerLayout.CardsPerBox);
        AssertBool(reopened.SequenceEqual(original)).IsTrue();
    }

    [TestCase]
    public static async Task ABoxSavedWhileItOpensIsSavedAsTheEightPacksItOpensInto()
    {
        using var scene = new SaveWorld("OpeningBoxWorld", addCardsToTree: false);
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.ContentSeed = BoxSeed;
        box.Spawning = scene.Spawner;
        scene.World.AddChild(box);
        var service = scene.Add(
            new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("a")) { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        box.Open();
        await ContainerOpeningWait.Until(() => box.SpawnedCount > 0, "the first pack leaving the box");

        // Some packs are still rising out of the box and the rest are still inside it when the save runs.
        AssertThat(box.PendingCount).IsGreater(0);
        service.SaveGame();
        AssertBool(box.IsQueuedForDeletion()).IsTrue();
        await scene.TwoFrames();

        service.LoadGame();
        await scene.TwoFrames();
        var restored = ContainersIn(scene.Tree);
        AssertThat(restored.Count(c => c.Kind == CardContainerKind.Box)).IsEqual(0);
        var seeds = restored.Where(c => c.Kind == CardContainerKind.Pack).Select(c => c.ContentSeed!.Value)
            .Order().ToList();
        var expected = Enumerable.Range(0, CardContainerLayout.ItemsPerContainer)
            .Select(i => ContainerSeeds.ChildSeed(BoxSeed, i)).Order().ToList();
        AssertThat(seeds).IsEqual(expected);
    }

    // Opens every closed container in the tree until no container is left. A box spawns packs, a pack spawns boosters
    // and a booster spawns cards, so the wait lasts until the whole chain has been opened. Only the cards matter here,
    // so each opening is finished at once instead of played out (the seeds decide the contents, not the animation).
    private static Task OpenUntilEmpty(SaveWorld scene) =>
        ContainerOpeningWait.Until(() =>
        {
            var containers = ContainersIn(scene.Tree);
            foreach (var container in containers.Where(c => c.CanInteract))
            {
                container.Open();
                container.FinishOpening();
            }

            return containers.Count == 0;
        }, "opening every container down to the cards");

    private static List<CardContainer> ContainersIn(SceneTree tree) =>
        tree.GetNodesInGroup("card_containers").OfType<CardContainer>().ToList();

    // Spawn order follows the tree, so the two boxes are compared as sorted lists of their signatures.
    private static List<string> Canonical(SpawnRecorder spawner) =>
        spawner.Spawned
            .Select(s => string.Join(",", s.Signature.Elements.Select(e => e.ToString("R", CultureInfo.InvariantCulture))))
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();
}
