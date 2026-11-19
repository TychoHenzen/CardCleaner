using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
/// Spawns saved cards back into the current scene, hands cards that were held back to the player's hand in the order
/// they were saved, and locks a card saved under a slot back into that slot.
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
        // A held card is built in the world and then taken into the hand, which places it in the fan; one the hand
        // does not take (no hand, or a full one) stays where it was saved.
        var spawnParent = saved.IsHeld ? _worldNode : ResolveSpawnParent(saved.ParentPath);
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
            Hand()?.AddCard(controller);
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

    // ASSUMPTION: one player has one hand (Risk Low), so the first card holder in the group takes the held cards.
    private ICardHolder? Hand() =>
        _tree.GetNodesInGroup(CardHolder.GroupName).OfType<ICardHolder>().FirstOrDefault();
}
