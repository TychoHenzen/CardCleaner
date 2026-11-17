using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
/// Spawns saved cards back into the current scene, hands cards that were held to the player camera, and locks a card
/// saved under a slot back into that slot.
/// </summary>
internal sealed class SavedCardRespawner
{
    private readonly SceneTree _tree;
    private readonly ICardSpawningService _spawningService;
    private readonly Node3D _worldNode;
    private readonly Action<CardController, Camera3D> _reparentToCamera;

    internal SavedCardRespawner(
        SceneTree tree,
        ICardSpawningService spawningService,
        Node3D worldNode,
        Action<CardController, Camera3D> reparentToCamera)
    {
        _tree = tree;
        _spawningService = spawningService;
        _worldNode = worldNode;
        _reparentToCamera = reparentToCamera;
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

        if (saved.IsHeld)
            ReparentWhenHeld(controller);
        // ASSUMPTION: a card is a slot's child only while the slot holds it. ReleaseCard leaves a card under its slot,
        // but its only card caller (CarriedStock) takes the card out of the tree at once, and a pickup moves it to the
        // camera. So a card saved under a slot goes back into that slot, at the seat the scene has now: a shelf whose
        // seats moved since the save still gets its cards back.
        // Locked now, not by the slot's trigger: a seat can lie inside its shelf's solid collider, which pushes a free
        // card off the seat, and out of the trigger, on the first physics step (see SavedContainers.Restore).
        else if (spawnParent is DeckSlot slot)
            slot.TryLock(controller);
    }

    private Node3D ResolveSpawnParent(string parentPath)
    {
        if (string.IsNullOrEmpty(parentPath))
            return _worldNode;

        return _tree.CurrentScene.GetNodeOrNull(parentPath) is Node3D parent3D ? parent3D : _worldNode;
    }

    // Find player camera and reparent if card was held
    private void ReparentWhenHeld(CardController controller)
    {
        var playerCamera = _tree
            .GetFirstNodeInGroup("player")
            ?.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (playerCamera != null)
            _reparentToCamera(controller, playerCamera);
    }
}
