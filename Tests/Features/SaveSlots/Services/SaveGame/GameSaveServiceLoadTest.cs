using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.SaveSlots.Services.SaveGame;

/// <summary>
/// Loads the committed pre-move save through the real save system (#172 U1). The game save service respawns each saved
/// card, and a card saved on a slot's seat is locked back into that slot. That save predates the hand record
/// (SavedHand), so its held card comes back loose in the world; the hand's own round trip is HandSaveTest.
/// The service reads and writes an in-memory slot file system, so no test touches the user folder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSaveServiceLoadTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string CardSlotScene = "res://Scenes/UI/CardSlot.tscn";

    [TestCase]
    public static async Task LoadingTheSaveRespawnsEachCard()
    {
        using var scene = new SaveWorld("GameSaveLoadWorld");
        var activeSlot = SaveWorld.SlotNamed("a");
        var files = new InMemorySlotFileSystem();
        var envelope = SaveEnvelope.Write(SavedCardFixture.ReadText(), DateTime.UtcNow);
        files.Seed(SaveSlotLayout.FileFor(activeSlot.Current), envelope);

        var service = scene.Add(new GameSaveService(files, activeSlot) { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();

        service.LoadGame();
        await scene.TwoFrames();

        AssertThat(scene.Spawner.Spawned).HasSize(2);
        AssertSpawn(scene.Spawner.Spawned[0], SavedCardFixture.HeldElements, SavedCardFixture.HeldPosition,
            SavedCardFixture.HeldRotation);
        AssertObject(scene.Spawner.Spawned[0].Card.GetParent()).IsSame(scene.World);
        AssertSpawn(scene.Spawner.Spawned[1], SavedCardFixture.PlacedElements, SavedCardFixture.PlacedPosition,
            SavedCardFixture.PlacedRotation);
        AssertObject(scene.Spawner.Spawned[1].Card.GetParent()).IsSame(scene.World);
    }

    [TestCase]
    public static async Task SavingAndLoadingAnAbsentSignaturePreservesAbsence()
    {
        using var scene = new SaveWorld("GameSaveAbsentWorld");
        scene.World.AddChild(new CardController { Name = "AbsentCard" });
        var service = scene.Add(new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("a"))
            { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        await scene.World.ToSignal(scene.Tree, SceneTree.SignalName.ProcessFrame);

        var save = new NodeSave();
        service.Save(save);
        AssertBool(save.TryGetProperty<List<object>>("cards", out var entries)).IsTrue();
        AssertThat(entries).HasSize(1);

        service.Load(save);
        await scene.TwoFrames();

        AssertThat(scene.Spawner.Spawned).HasSize(1);
        AssertBool(scene.Spawner.Spawned[0].Card.HasSignature).IsFalse();
    }

    [TestCase]
    public static async Task SavingAndLoadingALookSeedHandsItToTheSpawner()
    {
        using var scene = new SaveWorld("GameSaveLookWorld");
        scene.World.AddChild(
            new CardController { Name = "WithLook", Signature = new CardSignature(), LookSeed = 4242 });
        scene.World.AddChild(new CardController { Name = "WithoutLook", Signature = new CardSignature() });
        var service = scene.Add(new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("a"))
            { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        await scene.World.ToSignal(scene.Tree, SceneTree.SignalName.ProcessFrame);

        var save = new NodeSave();
        service.Save(save);
        scene.Spawner.Spawned.Clear();
        service.Load(save);
        await scene.TwoFrames();

        AssertThat(scene.Spawner.Spawned).HasSize(2);
        var seeds = scene.Spawner.Spawned.Select(s => s.LookSeed).ToList();
        AssertBool(seeds.Count(v => v is null) == 1).IsTrue();
        AssertThat(seeds.Single(v => v.HasValue)!.Value).IsEqual(4242);
    }

    // A card saved under a slot is locked back into the slot as it respawns, before any physics step: a seat can lie
    // inside its shelf's solid collider, which pushes a free card off it before the slot's trigger catches it. It goes
    // to the seat the slot has now, so a shelf whose seats moved since the save (the card display replaced the old
    // card shelf) still holds its cards. A card the full slot cannot take stays free.
    [TestCase]
    public static void ACardSavedUnderASlotIsLockedIntoTheSlotsCurrentSeatAsItRespawns()
    {
        using var scene = new SaveWorld("GameSaveSeatWorld");
        var slot = GD.Load<PackedScene>(CardSlotScene).Instantiate<DeckSlot>();
        slot.Capacity = 1;
        scene.World.AddChild(slot);
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        var respawner = new SavedCardRespawner(scene.Tree, scene.Spawner, scene.World);
        var slotPath = slot.GetPath().ToString();

        var oldSeat = slot.SeatPosition(0) + new Vector3(0.5f, 0.3f, -0.2f);
        respawner.Respawn(new SavedCardRecord(new CardSignature(), oldSeat, Vector3.Zero, slotPath));
        respawner.Respawn(new SavedCardRecord(new CardSignature(), oldSeat, Vector3.Zero, slotPath));

        var seated = scene.Spawner.Spawned[0].Card;
        var extra = scene.Spawner.Spawned[1].Card;
        AssertThat(slot.Cards.Count).IsEqual(1);
        AssertObject(slot.Cards[0]).IsSame(seated);
        AssertBool(seated.Freeze).IsTrue();
        AssertThat(seated.GlobalPosition.DistanceTo(slot.SeatPosition(0))).IsLess(0.001f);
        AssertBool(extra.Freeze).IsFalse();
    }

    // A reload into the same scene frees the shelved card and respawns its saved copy. The freed card must leave the
    // slot before the copy respawns: a card-shelf slot holds one card, so a freed card still counted in it would keep
    // the copy out.
    [TestCase]
    public static async Task ReloadingPutsTheSavedCopyOfAShelvedCardIntoItsOneCardSlot()
    {
        using var scene = new SaveWorld("GameSaveShelvedWorld");
        var slot = GD.Load<PackedScene>(CardSlotScene).Instantiate<DeckSlot>();
        slot.Capacity = 1;
        scene.World.AddChild(slot);
        var shelved = new CardController { Name = "Shelved", Signature = new CardSignature() };
        scene.World.AddChild(shelved);
        var service = scene.Add(new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("a"))
            { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        await scene.World.ToSignal(scene.Tree, SceneTree.SignalName.ProcessFrame);
        AssertBool(slot.TryLock(shelved)).IsTrue();

        var save = new NodeSave();
        service.Save(save);
        service.Load(save);
        await scene.TwoFrames();

        AssertThat(scene.Spawner.Spawned).HasSize(1);
        AssertThat(slot.Cards.Count).IsEqual(1);
        AssertObject(slot.Cards[0]).IsSame(scene.Spawner.Spawned[0].Card);
    }

    [TestCase]
    public static void MalformedContainerRecordLeavesTheSceneContainersInPlace()
    {
        using var scene = new SaveWorld("GameSaveMalformedWorld");
        var existing = GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();
        scene.World.AddChild(existing);
        var service = scene.Add(
            new GameSaveService(new InMemorySlotFileSystem(), SaveWorld.SlotNamed("a")) { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();
        var save = new NodeSave();
        save.SetOrAddProperty("containers", new List<object>
        {
            new Dictionary<string, object> { ["ScenePath"] = BoosterScene, ["Kind"] = "NotAKind" },
        });

        FormatException? thrown = null;
        try
        {
            service.Load(save);
        }
        catch (FormatException ex)
        {
            thrown = ex;
        }

        AssertObject(thrown).IsNotNull();
        AssertBool(existing.IsQueuedForDeletion()).IsFalse();
    }

    private static void AssertSpawn(SpawnRecorder.Spawn spawn, float[] elements, Vector3 position, Vector3 rotation)
    {
        AssertThat(spawn.Signature.Elements).IsEqual(elements);
        AssertThat(spawn.Transform.Origin).IsEqual(position);
        AssertThat(spawn.Transform.Basis).IsEqual(Basis.FromEuler(rotation));
        AssertThat(spawn.LookSeed).IsNull();
    }
}
