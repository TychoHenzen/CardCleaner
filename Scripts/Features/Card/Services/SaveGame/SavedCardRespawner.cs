using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
/// Spawns saved cards back into the current scene and locks a card saved under a slot back into that slot. Cards the
/// hand held come back through <see cref="SavedHand" />.
/// </summary>
internal sealed class SavedCardRespawner
{
    private readonly SceneTree _tree;
    private readonly ICardSpawningService _spawningService;
    private readonly Node3D _worldNode;

    internal SavedCardRespawner(SceneTree tree, ICardSpawningService spawningService, Node3D worldNode)
    {
        _tree = tree;
        _spawningService = spawningService;
        _worldNode = worldNode;
    }

    internal void Respawn(SavedCardRecord saved)
    {
        var spawnTransform = new Transform3D(Basis.FromEuler(saved.Rotation), saved.Position);
        var spawnParent = ResolveSpawnParent(saved.ParentPath);
        var cardInstance = _spawningService.SpawnCard(
            saved.Signature ?? new CardSignature(),
            saved.LookSeed,
            spawnTransform,
            spawnParent);

        if (cardInstance is not CardController controller)
            return;

        if (!saved.Signature.HasValue)
            controller.ClearSignature();

        // Before the slot lock below, so the slot's price tag shows this price when it repaints.
        controller.ShopPrice = saved.Price;

        // ASSUMPTION: a card is a slot's child only while the slot holds it. ReleaseCard leaves a card under its slot,
        // but its only card caller (CarriedStock) takes the card out of the tree at once, and a pickup moves it to the
        // camera. So a card saved under a slot goes back into that slot, at the seat the scene has now: a shelf whose
        // seats moved since the save still gets its cards back.
        // Locked now, not by the slot's trigger: a seat can lie inside its shelf's solid collider, which pushes a free
        // card off the seat, and out of the trigger, on the first physics step (see SavedContainers.Restore).
        if (spawnParent is DeckSlot slot)
            slot.TryLock(controller);
    }

    private Node3D ResolveSpawnParent(string parentPath)
    {
        if (string.IsNullOrEmpty(parentPath))
            return _worldNode;

        // A card under the camera was held. The hand's record brings held cards back now, so a card list entry under
        // the camera comes from a save made before that record and loads loose in the world, never as a card hanging
        // in view that the hand does not hold.
        return _tree.CurrentScene.GetNodeOrNull(parentPath) is Node3D parent3D and not Camera3D
            ? parent3D
            : _worldNode;
    }
}
