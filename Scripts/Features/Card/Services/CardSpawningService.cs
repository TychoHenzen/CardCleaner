using Godot;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using System;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
/// Handles spawning of card instances with proper generation and setup.
/// </summary>
[Service(typeof(ICardSpawningService))]
public partial class CardSpawningService : Node, ICardSpawningService, IServiceProvider, IDependent
{
    /// <summary>
    /// Emitted when a new card is spawned, allowing other systems to connect to it
    /// </summary>
    public event Action<CardController>? CardSpawned;

    [Export] public PackedScene? CardScene;
    private ICardGenerator _generator = null!;
    private IRandom _rng = null!;
    private IServiceResolver? _services;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        AddToGroup("service_providers");
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IRandom>(rng => _rng = rng);
        _services.Get<ICardGenerator>(gen => _generator = gen);
    }

    public void RegisterServices(IServiceContainer container)
    {
        container.RegisterSingleton<ICardSpawningService>(this);
    }

    public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
        => SpawnCard(signature, null, spawnTransform, parent);

    public Node3D? SpawnCard(
        CardSignature signature, CardSignature? visualSignature, Transform3D spawnTransform, Node3D parent)
    {
        if (CardScene?.Instantiate() is not Node3D cardInstance)
            return null;

        // Add to parent and set transform
        parent.AddChild(cardInstance);
        cardInstance.GlobalTransform = spawnTransform;
        cardInstance.Name = "Card";

        var visual = visualSignature ?? CardVisualSignature.For(signature, _rng);

        // Set up card controller with signature
        if (cardInstance is CardController controller)
        {
            controller.Signature = signature;
            controller.VisualSignature = visual;

            // Emit signal so other systems can connect to this new card
            CallDeferred(MethodName.EmitCardSpawnedSignal, controller);
        }

        // Set up card renderer with deferred baking
        var renderer = cardInstance.GetNodeOrNull<CardShaderRenderer>("CardRenderer");
        if (renderer != null)
            // Use CallDeferred to ensure all components are ready
            Callable.From(() => BakeCardRenderer(renderer, signature, visual)).CallDeferred();

        return cardInstance;
    }

    public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent)
    {
        var signature = SignatureRandom.Create(_rng);
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

    private void BakeCardRenderer(CardShaderRenderer renderer, CardSignature signature, CardSignature visual)
    {
        var template = new CardTemplate();
        _generator.GenerateCardRenderer(renderer, signature, visual, template);
        renderer.Bake(template);
    }

    private void EmitCardSpawnedSignal(CardController card)
    {
        CardSpawned?.Invoke(card);
    }
}
