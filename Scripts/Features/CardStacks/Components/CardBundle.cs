using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     Many cards as one physics body: a strapped deck (a neat block with a strap round it) or a pile (a low mound of
///     cards lying every which way). It keeps every card as a <see cref="StoredCard" /> (signature, look seed and
///     price) in the order they came in, and grows or shrinks with its count. Its origin is the middle of its base.
///     A click asks the player's hand to take cards (<see cref="TakeRequested" />): a deck is unstrapped whole, a pile
///     gives the hand as many cards as fit, from the last in, and is freed once it is empty.
/// </summary>
public partial class CardBundle : RigidBody3D, IInteractable
{
    /// <summary>Every deck and pile joins this group, so a save finds them.</summary>
    public const string GroupName = "card_bundles";

    /// <summary>Raised when the player clicks a bundle the hand can take cards from.</summary>
    [Signal]
    public delegate void TakeRequestedEventHandler(CardBundle bundle);

    private const uint WorldLayer = 1;
    private const uint InteractableLayer = 4; // Layer 3, in the InteractionSystem mask
    private const float Reach = 4.5f;
    private const float HighlightMargin = 0.04f;
    private const string HighlightMaterialPath = "res://Data/Ui/AimHighlightMaterial.tres";

    // ASSUMPTION: a card weighs 50 g (Risk Low). Nothing else in the game weighs cards; the value only has to keep a
    // big pile heavy enough not to be shoved about by a single card landing on it.
    private const float CardMass = 0.05f;

    private readonly List<StoredCard> _cards = new();
    private readonly CollisionShape3D _shape = new() { Name = "Shape" };
    private readonly Node3D _look = new() { Name = "Look" };
    private readonly MeshInstance3D _highlight = new() { Name = "Highlight", Visible = false };

    public CardBundleKind Kind { get; private set; }

    /// <summary>The cards, first in first.</summary>
    public IReadOnlyList<StoredCard> Cards => _cards;

    public int Count => _cards.Count;

    /// <summary>How many more cards the player's hand takes now. The hand sets it; null means none.</summary>
    public Func<int>? HandRoom { get; set; }

    /// <summary>How many cards a click takes into the hand now (<see cref="CardBundleRules.TakeCount" />).</summary>
    public int TakeCount => CardBundleRules.TakeCount(Kind, Count, HandRoom?.Invoke() ?? 0);

    public bool CanInteract => Count > 0 && TakeCount > 0;

    public Node3D InteractionBody => this;

    public float InteractionRange => Reach;

    public string Prompt => CardBundleRules.Prompt(Kind, Count, TakeCount);

    /// <summary>A new deck or pile of <paramref name="cards" />, not yet in the tree.</summary>
    public static CardBundle Create(CardBundleKind kind, IEnumerable<StoredCard> cards)
    {
        var bundle = new CardBundle { Kind = kind, Name = kind.ToString() };
        bundle._cards.AddRange(cards);
        bundle.AddChild(bundle._shape);
        bundle.AddChild(bundle._look);
        bundle.AddChild(bundle._highlight);
        bundle.Rebuild();
        return bundle;
    }

    public override void _EnterTree() => AddToGroup(GroupName);

    public override void _Ready()
    {
        CollisionLayer = WorldLayer | InteractableLayer;
        CollisionMask = WorldLayer;
    }

    /// <summary>Adds <paramref name="cards" /> on top and grows to hold them.</summary>
    public void Add(IEnumerable<StoredCard> cards)
    {
        _cards.AddRange(cards);
        Rebuild();
    }

    /// <summary>
    ///     Takes the last <paramref name="count" /> cards in (all of them at most), in the order they came in. A bundle
    ///     left empty leaves the tree at once and is freed.
    /// </summary>
    public IReadOnlyList<StoredCard> Take(int count)
    {
        var take = Math.Clamp(count, 0, _cards.Count);
        var taken = _cards.GetRange(_cards.Count - take, take);
        _cards.RemoveRange(_cards.Count - take, take);
        if (_cards.Count == 0)
        {
            GetParent()?.RemoveChild(this);
            QueueFree();
        }
        else
        {
            Rebuild();
        }

        return taken;
    }

    public void Interact()
    {
        if (CanInteract)
            EmitSignal(SignalName.TakeRequested, this);
    }

    public void Highlight() => _highlight.Visible = true;

    public void ClearHighlight() => _highlight.Visible = false;

    // Shape, look, outline and mass all follow the count.
    private void Rebuild()
    {
        // Plain meshes nothing else holds: freed at once, so no old look waits outside the tree for the frame's end.
        foreach (var child in _look.GetChildren())
            child.Free();

        var bounds = Kind == CardBundleKind.Deck ? BuildDeck() : BuildPile();
        _highlight.Mesh = new BoxMesh
        {
            Size = bounds + Vector3.One * HighlightMargin,
            Material = GD.Load<Material>(HighlightMaterialPath)
        };
        _highlight.Position = new Vector3(0f, bounds.Y / 2f, 0f);
        Mass = Math.Max(CardMass, Count * CardMass);
    }

    private Vector3 BuildDeck()
    {
        var size = CardBundleRules.DeckSize(Count);
        _shape.Shape = new BoxShape3D { Size = size };
        _shape.Position = new Vector3(0f, size.Y / 2f, 0f);
        CardBundleLook.AddDeck(_look, size);
        return size;
    }

    private Vector3 BuildPile()
    {
        var radius = CardBundleRules.PileRadius(Count);
        var height = CardBundleRules.PileHeight(Count);
        _shape.Shape = new ConvexPolygonShape3D { Points = CardBundleLook.DomePoints(radius, height) };
        _shape.Position = Vector3.Zero;
        CardBundleLook.AddPile(_look, radius, height, Count, PileSeed());
        return new Vector3(2f * radius, height, 2f * radius);
    }

    // The same cards always heap the same way, so a reload shows the pile it saved.
    private int PileSeed() =>
        _cards.Aggregate(Count, (seed, card) => unchecked(seed * 31 + (card.LookSeed ?? 0)));
}
