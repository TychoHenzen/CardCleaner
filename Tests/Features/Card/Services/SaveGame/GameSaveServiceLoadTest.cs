using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.SaveSlots;
using GdUnit4;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Card.Services.SaveGame;

/// <summary>
/// Loads the committed pre-move save through the real save system (#172 U1). The game save service respawns each saved
/// card, and the held card is handed to the player camera. The service reads and writes an in-memory slot file system,
/// so no test touches the user folder.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSaveServiceLoadTest
{
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";

    [TestCase]
    [TestCategory("Unit")]
    public static async Task LoadingTheSaveRespawnsEachCardAndHandsTheHeldOneToTheCamera()
    {
        using var scene = new SaveWorld("GameSaveLoadWorld");
        var activeSlot = SaveWorld.SlotNamed("a");
        var files = new InMemorySlotFileSystem();
        var envelope = SaveEnvelope.Write(SavedCardFixture.ReadText(), DateTime.UtcNow);
        files.Seed(SaveSlotLayout.FileFor(activeSlot.Current), envelope);

        var player = new Node3D { Name = "Player" };
        var head = new Node3D { Name = "Head" };
        var camera = new Camera3D { Name = "Camera3D" };
        player.AddToGroup("player");
        head.AddChild(camera);
        player.AddChild(head);
        scene.World.AddChild(player);
        var service = scene.Add(new GameSaveService(files, activeSlot) { AutoLoadOnStart = false });
        AddNode(scene.World, autoFree: false);
        scene.Enter();

        service.LoadGame();
        await scene.TwoFrames();

        AssertThat(scene.Spawner.Spawned).HasSize(2);
        AssertSpawn(scene.Spawner.Spawned[0], SavedCardFixture.HeldElements, SavedCardFixture.HeldPosition,
            SavedCardFixture.HeldRotation);
        AssertObject(scene.Spawner.Spawned[0].Card.GetParent()).IsSame(camera);
        AssertSpawn(scene.Spawner.Spawned[1], SavedCardFixture.PlacedElements, SavedCardFixture.PlacedPosition,
            SavedCardFixture.PlacedRotation);
        AssertObject(scene.Spawner.Spawned[1].Card.GetParent()).IsSame(scene.World);
    }

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
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

    [TestCase]
    [TestCategory("Unit")]
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
