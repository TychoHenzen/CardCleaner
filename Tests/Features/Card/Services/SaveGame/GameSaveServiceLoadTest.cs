using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using GdUnit4;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Card.Services.SaveGame;

/// <summary>
/// Loads the committed pre-move save through the real save system (#172 U1). The game save service respawns each saved
/// card, and the held card is handed to the player camera. The service saves to the test's own GUID-named file only.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSaveServiceLoadTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static async Task LoadingTheSaveRespawnsEachCardAndHandsTheHeldOneToTheCamera()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousScene = tree.CurrentScene;
        var spawner = new RecordingSpawner();
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<ICardSpawningService>(spawner);

        var savePath = $"user://game-save-load-{Guid.NewGuid():N}.json";
        var world = new Node3D { Name = "GameSaveLoadWorld" };
        var player = new Node3D { Name = "Player" };
        var head = new Node3D { Name = "Head" };
        var camera = new Camera3D { Name = "Camera3D" };
        player.AddToGroup("player");
        head.AddChild(camera);
        player.AddChild(head);
        var service = new GameSaveService { AutoLoadOnStart = false, SavePath = savePath };
        world.AddChild(player);
        world.AddChild(service);
        AddNode(world, autoFree: false);

        try
        {
            tree.CurrentScene = world;
            WriteSaveFile(savePath);
            SaveSystem.LoadFile(savePath, world);

            // Load defers RecreateCards, and RecreateCards defers the held card's reparent: wait for both.
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await world.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(spawner.Spawned).HasSize(2);
            var held = spawner.Spawned[0];
            AssertThat(held.Signature.Elements).IsEqual(SavedCardFixture.HeldElements);
            AssertThat(held.Transform.Origin).IsEqual(SavedCardFixture.HeldPosition);
            AssertThat(held.Transform.Basis).IsEqual(Basis.FromEuler(SavedCardFixture.HeldRotation));
            AssertObject(held.Card.GetParent()).IsSame(camera);

            var placed = spawner.Spawned[1];
            AssertThat(placed.Signature.Elements).IsEqual(SavedCardFixture.PlacedElements);
            AssertThat(placed.Transform.Origin).IsEqual(SavedCardFixture.PlacedPosition);
            AssertThat(placed.Transform.Basis).IsEqual(Basis.FromEuler(SavedCardFixture.PlacedRotation));
            AssertObject(placed.Card.GetParent()).IsSame(world);
        }
        finally
        {
            // GameSaveService._ExitTree saves to savePath. Removing the service first runs that save while the
            // cards are still in the tree; the temp file is deleted only after it has run.
            world.RemoveChild(service);
            service.Free();
            world.Free();
            tree.CurrentScene = previousScene;
            DeleteSaveFile(savePath);
            ServiceLocator.ResetForTesting();
        }
    }

    private static void WriteSaveFile(string path)
    {
        var file = FileAccess.Open(path, FileAccess.ModeFlags.Write)
            ?? throw new InvalidOperationException($"Cannot write {path}");
        file.StoreString(SavedCardFixture.ReadText());
        file.Close();
    }

    private static void DeleteSaveFile(string path)
    {
        // Removes only this GUID-named file. DirAccess.Remove does not recurse.
        DirAccess.Open("user://")?.Remove(System.IO.Path.GetFileName(path));
    }

    /// <summary>Records each respawn and adds the card under its parent, as the real spawner does.</summary>
    private sealed class RecordingSpawner : ICardSpawningService
    {
        public List<Spawn> Spawned { get; } = [];

        public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
        {
            var card = new CardController { Signature = signature };
            parent.AddChild(card);
            Spawned.Add(new Spawn(signature, spawnTransform, card));
            return card;
        }

        public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

        public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
    }

    private sealed record Spawn(CardSignature Signature, Transform3D Transform, CardController Card);
}
