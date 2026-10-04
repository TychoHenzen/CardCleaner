using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Spawns saved cards back into the current scene and hands cards that were held to the player camera.
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
        var cardInstance = _spawningService.SpawnCard(saved.Signature, spawnTransform, spawnParent);

        if (saved.IsHeld && cardInstance is CardController controller)
            ReparentWhenHeld(controller);
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
