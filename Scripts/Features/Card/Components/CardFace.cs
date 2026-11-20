using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     What a card looks like and nothing else (<c>Scenes/Components/CardFace.tscn</c>): the baked card mesh with its
///     layered material, the rarity name and the flavor sentence. A card body shows one, and a pile shows one for each
///     card on top, so a card looks the same lying loose or in a pile. It has no body, collision or script of the
///     card's own, so a face in a pile is never a card the game counts. <see cref="Dress" /> paints it from a
///     signature and look seed, the same way for both.
/// </summary>
public partial class CardFace : Node3D
{
    /// <summary>The scene of a face, which the card scene instances as its <c>Face</c> child.</summary>
    public const string ScenePath = "res://Scenes/Components/CardFace.tscn";

    private readonly List<ICardComponent> _components = new();
    private bool _setUp;

    /// <summary>The renderer that paints the face's material and labels.</summary>
    public CardShaderRenderer Renderer => GetNode<CardShaderRenderer>("CardRenderer");

    /// <summary>The face's parts that a card body drives each physics frame (the blacklight).</summary>
    public IEnumerable<IPhysicsComponent> PhysicsComponents
    {
        get
        {
            SetUp();
            return _components.OfType<IPhysicsComponent>();
        }
    }

    public override void _Ready() => SetUp();

    /// <summary>
    ///     Paints the face for <paramref name="signature" /> and <paramref name="lookSeed" />: the generator picks its
    ///     layers, gems, art effects and text, and the renderer bakes them into the material on the next frame.
    /// </summary>
    public void Dress(ICardGenerator generator, CardSignature signature, int? lookSeed)
    {
        SetUp();
        var template = new CardTemplate();
        generator.GenerateCardRenderer(Renderer, signature, lookSeed, template);
        Renderer.Bake(template);
    }

    // Each part finds its siblings through this root, whether or not the face is in the tree yet: a pile dresses its
    // faces before it enters the tree, and a renderer without its material manager would wait for it every frame.
    private void SetUp()
    {
        if (_setUp)
            return;

        _setUp = true;
        foreach (var component in GetChildren().OfType<ICardComponent>())
        {
            component.Setup(this);
            _components.Add(component);
        }
    }
}
