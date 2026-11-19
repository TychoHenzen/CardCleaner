using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
/// Saves a cardboard box through the save service and reloads it from the slot. A fresh box comes back fresh and
/// gives out the same booster boxes; a box something went into or came out of comes back holding the same items in
/// the same order. The in-memory slot file system keeps the test off the user folder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SealedBoxSaveTest
{
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string PackScene = "res://Scenes/Shop/Items/Pack.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const ulong BoxSeed = 0x5EA1EDUL;

    [TestCase]
    public static async Task AFreshBoxReloadsFreshAndGivesOutTheSameFirstBoosterBox()
    {
        using var scene = new SaveWorld("FreshBoxWorld", addCardsToTree: false);
        var service = SavedBox(scene, null);
        service.SaveGame();

        service.LoadGame();
        await scene.TwoFrames();

        var reloaded = OnlyBox(scene);
        AssertThat(reloaded.ContentSeed).IsEqual(BoxSeed);
        AssertBool(reloaded.Storage!.IsFresh).IsTrue();
        AssertThat(reloaded.Remaining).IsEqual(CardContainerLayout.ItemsPerContainer);

        reloaded.SetLidOpen(true, instantly: true);
        AssertBool(reloaded.Storage.TakeOnto(new Transform3D(Basis.Identity, new Vector3(3f, 0f, 0f)))).IsTrue();
        var taken = scene.Tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Single(container => container.Kind == CardContainerKind.Pack);
        AssertThat(taken.ContentSeed).IsEqual(ContainerSeeds.ChildSeed(BoxSeed, 0));
    }

    [TestCase]
    public static async Task AMixedBoxReloadsHoldingTheSameItemsInTheSameOrder()
    {
        using var scene = new SaveWorld("MixedBoxWorld", addCardsToTree: false);
        var signature = new CardSignature([0.5f, -0.25f, 0f, 1f, -1f, 0.125f, 0.75f, -0.5f]);
        var items = new List<StoredItem>
        {
            new StoredCard(signature, 41, 12),
            new StoredContainer(BoosterScene, CardContainerKind.Booster, 99UL, 4),
            new StoredContainer(PackScene, CardContainerKind.Pack, 7UL, null),
            new StoredCard(null, 3, null),
        };
        var service = SavedBox(scene, items);
        service.SaveGame();

        service.LoadGame();
        await scene.TwoFrames();

        var reloaded = OnlyBox(scene);
        AssertBool(reloaded.Storage!.IsFresh).IsFalse();
        AssertThat(StoredItemKeys.Of(reloaded.Storage.Items)).IsEqual(StoredItemKeys.Of(items));
    }

    [TestCase]
    public static async Task AnEmptiedBoxReloadsEmptyNotFresh()
    {
        using var scene = new SaveWorld("EmptyBoxWorld", addCardsToTree: false);
        var service = SavedBox(scene, []);
        service.SaveGame();

        service.LoadGame();
        await scene.TwoFrames();

        var reloaded = OnlyBox(scene);
        AssertBool(reloaded.Storage!.IsFresh).IsFalse();
        AssertThat(reloaded.Remaining).IsEqual(0);
    }

    // A seeded box in a world with a save service, holding items when they are given (a fresh box otherwise).
    private static GameSaveService SavedBox(SaveWorld scene, IReadOnlyList<StoredItem>? items)
    {
        var box = GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>();
        box.ContentSeed = BoxSeed;
        box.Spawning = scene.Spawner;
        box.Storage!.Restore(items);
        scene.World.AddChild(box);
        var service = scene.Add(
            new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("a")) { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        return service;
    }

    private static CardContainer OnlyBox(SaveWorld scene)
    {
        var boxes = scene.Tree.GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(container => container.Kind == CardContainerKind.Box && !container.IsQueuedForDeletion())
            .ToList();
        AssertThat(boxes).HasSize(1);
        return boxes.Single();
    }
}
