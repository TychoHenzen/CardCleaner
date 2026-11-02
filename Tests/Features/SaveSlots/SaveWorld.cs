using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Services.SaveGame;
using CardCleaner.Scripts.Features.SaveSlots.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
///     The scene a save-slot test runs in: a world node that becomes the current scene, a save service in it, and the
///     bookkeeping to put the previous scene back. <see cref="Dispose" /> removes the service first so its exit save runs
///     while the cards are still in the tree.
/// </summary>
internal sealed class SaveWorld : IDisposable
{
    private readonly Node? _previousScene;

    /// <param name="name">Name of the world node.</param>
    /// <param name="addCardsToTree">Whether the registered spawner adds each card it spawns under its parent.</param>
    public SaveWorld(string name, bool addCardsToTree = true)
    {
        TestServices.Reset();
        Spawner = new SpawnRecorder(addCardsToTree);
        TestServices.Container.RegisterSingleton<ICardSpawningService>(Spawner);
        Tree = (SceneTree)Engine.GetMainLoop();
        _previousScene = Tree.CurrentScene;
        World = new Node3D { Name = name };
    }

    public SceneTree Tree { get; }

    /// <summary>The card spawner registered for the service to resolve.</summary>
    public SpawnRecorder Spawner { get; }

    public Node3D World { get; }

    public GameSaveService? Service { get; private set; }

    /// <summary>A slot holder with the slot named <paramref name="raw" /> active.</summary>
    public static ActiveSaveSlot SlotNamed(string raw)
    {
        var slots = new ActiveSaveSlot();
        Activate(slots, raw);
        return slots;
    }

    public static void Activate(ActiveSaveSlot slots, string raw)
    {
        if (!SaveSlotName.TryCreate(raw, out var name, out var reason))
            throw new InvalidOperationException(reason);

        slots.Activate(name);
    }

    /// <summary>Injects <paramref name="service" /> like the autoload would and adds it to the world.</summary>
    public GameSaveService Add(GameSaveService service)
    {
        Service = TestServices.Inject(service);
        World.AddChild(Service);
        return Service;
    }

    public void Enter() => Tree.CurrentScene = World;

    /// <summary>Waits two frames: load defers the card respawn, and the respawn defers the held card's reparent.</summary>
    public async Task TwoFrames()
    {
        await World.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
        await World.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    public void Dispose()
    {
        if (Service != null)
        {
            World.RemoveChild(Service);
            Service.Free();
        }

        World.Free();
        Tree.CurrentScene = _previousScene;
        TestServices.Reset();
    }
}
