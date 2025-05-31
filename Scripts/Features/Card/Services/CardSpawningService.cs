using Godot;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
/// Handles spawning of card instances with proper generation and setup.
/// </summary>
public partial class CardSpawningService : Node, ICardSpawningService
{
    [Export] public PackedScene CardScene;
    private ICardGenerator _generator;
    private RandomNumberGenerator _rng;

    public override void _Ready()
    {
        ServiceLocator.Get<RandomNumberGenerator>(rng => _rng = rng);
        ServiceLocator.Get<ICardGenerator>(gen => _generator = gen);
    }
    public Node3D SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
    {
        if (CardScene.Instantiate() is not Node3D cardInstance)
            return null;

        // Add to parent and set transform
        parent.AddChild(cardInstance);
        cardInstance.GlobalTransform = spawnTransform;
        cardInstance.Name = "Card";

        // Set up card controller with signature
        if (cardInstance is CardController controller)
        {
            controller.Signature = signature;
        }

        // Set up card renderer with deferred baking
        var renderer = cardInstance.GetNodeOrNull<CardShaderRenderer>("CardRenderer");
        if (renderer != null)
        {
            // Use CallDeferred to ensure all components are ready
            CallDeferred(nameof(BakeCardRenderer), renderer, signature);
        }

        return cardInstance;
    }

    public Node3D SpawnRandomCard(Transform3D spawnTransform, Node3D parent)
    {
        var signature = CardSignature.Random(_rng);
        return SpawnCard(signature, spawnTransform, parent);
    }

    public Vector3 GetRandomOffset(Vector3 offsetRange)
    {
        return new Vector3(
            (_rng.Randf() * 2 - 1) * offsetRange.X,
            (_rng.Randf() * 2 - 1) * offsetRange.Y,
            (_rng.Randf() * 2 - 1) * offsetRange.Z
        );
    }

    private void BakeCardRenderer(CardShaderRenderer renderer, CardSignature signature)
    {
        var template = new CardTemplate();
        _generator.GenerateCardRenderer(renderer, signature, template);
        renderer.Bake(template);
    }
}