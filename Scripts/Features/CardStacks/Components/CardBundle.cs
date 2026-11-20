using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     Many cards as one physics body: a strapped deck (a neat block with a strap round it) or a pile (a low mound of
///     cards lying every which way). It keeps every card as a <see cref="StoredCard" /> (signature, look seed and
///     price) in the order they came in, and grows or shrinks with its count. Its origin is the middle of its base.
///     It is a store (<see cref="IItemStore" />): the player's hand router decides what a click on it takes (a deck is
///     unstrapped whole when every card fits, a pile gives one card, or with Shift as many as fit, from the last in),
///     and it is freed once it is empty. The bundle itself offers no click (<see cref="CanInteract" /> is false). A
///     pile lays its cards by its fixed <see cref="PileSeed" /> (<see cref="PileLook" />), and a free card that touches
///     it (<see cref="LooseCards.IsFree" />) joins it on the next physics frame through its join area.
/// </summary>
public partial class CardBundle : RigidBody3D, IInteractable, IItemStore
{
    /// <summary>Every deck and pile joins this group, so a save finds them.</summary>
    public const string GroupName = "card_bundles";

    private const uint WorldLayer = 1;
    private const uint InteractableLayer = 4; // Layer 3, in the InteractionSystem mask

    // Loose cards lie on layer 2 (collision_layer of Scenes/Components/CardShader.tscn).
    private const uint CardLayer = 2;

    // The join area reaches this far past the dome, so a card resting on the dome's surface overlaps it.
    private const float JoinMargin = 0.05f;
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
    private readonly List<CardController> _joining = new();
    private PileLook? _pileLook;
    private CollisionShape3D? _joinShape;

    public CardBundleKind Kind { get; private set; }

    /// <summary>
    ///     The seed a pile lays its cards by (<see cref="PileLayout" />), fixed when the bundle is made and saved with
    ///     its cards, so every card keeps its place as the pile grows, shrinks or reloads.
    /// </summary>
    public int PileSeed { get; private set; }

    /// <summary>The node drawn for each card a pile shows, by its index in join order (none for a deck).</summary>
    internal IReadOnlyDictionary<int, CardFace> ShownCards =>
        _pileLook?.Shown ?? (IReadOnlyDictionary<int, CardFace>)new Dictionary<int, CardFace>();

    /// <summary>The cards, first in first.</summary>
    public IReadOnlyList<StoredCard> Cards => _cards;

    public int Count => _cards.Count;

    public bool CanInteract => false;

    public Node3D InteractionBody => this;

    public float InteractionRange => Reach;

    public string Prompt => string.Empty;

    /// <summary>
    ///     A new deck or pile of <paramref name="cards" />, not yet in the tree, laid with <paramref name="seed" />, or
    ///     when none is given with a seed from the cards it starts with (<see cref="PileLayout.SeedFor" />).
    /// </summary>
    public static CardBundle Create(CardBundleKind kind, IEnumerable<StoredCard> cards, int? seed = null)
    {
        var bundle = new CardBundle { Kind = kind, Name = kind.ToString() };
        bundle._cards.AddRange(cards);
        bundle.PileSeed = seed ?? PileLayout.SeedFor(bundle._cards.Select(card => card.LookSeed).ToList());
        bundle.AddChild(bundle._shape);
        bundle.AddChild(bundle._look);
        bundle.AddChild(bundle._highlight);
        if (kind == CardBundleKind.Pile)
            bundle.AddJoinArea();
        bundle.Rebuild();
        return bundle;
    }

    public override void _EnterTree() => AddToGroup(GroupName);

    public override void _Ready()
    {
        CollisionLayer = WorldLayer | InteractableLayer;
        CollisionMask = WorldLayer;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_joining.Count == 0)
            return;

        var cards = _joining.Distinct().Where(card => IsInstanceValid(card) && LooseCards.IsFree(card)).ToList();
        _joining.Clear();
        if (cards.Count > 0 && LooseCards.IsAlive(this))
            Add(LooseCards.TakeRecords(cards));
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

    /// <summary>The card a take gives next: the last one in.</summary>
    public StoredItem? Peek() => _cards.Count > 0 ? _cards[^1] : null;

    /// <summary>
    ///     A deck or pile keeps cards only, and an emptied one, already gone from the tree, takes nothing.
    /// </summary>
    public bool CanPut(StoredItem item) => item is StoredCard && _cards.Count > 0;

    public bool Put(StoredItem item)
    {
        if (!CanPut(item))
            return false;

        Add(new[] { (StoredCard)item });
        return true;
    }

    /// <summary>Takes the last card in; the bundle is freed once its last card is taken.</summary>
    public StoredItem? TakeLast() => _cards.Count > 0 ? Take(1)[0] : null;

    // The hand router plans every click on a bundle (it claims this target).
    public void Interact()
    {
    }

    public void Highlight() => _highlight.Visible = true;

    public void ClearHighlight() => _highlight.Visible = false;

    // Shape, look, outline and mass all follow the count.
    private void Rebuild()
    {
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
        // Plain meshes nothing else holds: freed at once, so no old look waits outside the tree for the frame's end.
        foreach (var child in _look.GetChildren())
            child.Free();

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
        _joinShape!.Shape = new ConvexPolygonShape3D
        {
            Points = CardBundleLook.DomePoints(radius + JoinMargin, height + JoinMargin)
        };
        if (_pileLook == null)
        {
            _pileLook = new PileLook { Name = "Pile" };
            _look.AddChild(_pileLook);
        }

        _pileLook.Show(_cards, PileSeed);
        return new Vector3(2f * radius, height, 2f * radius);
    }

    // An area over the dome that sees loose cards only and that nothing sees (no layer), so the interaction ray,
    // furniture probes and delivery stacking pass through it. A card that touches it is joined in the next
    // _PhysicsProcess, not in the signal, which fires while the physics step is being flushed.
    private void AddJoinArea()
    {
        var area = new Area3D { Name = "JoinArea", CollisionLayer = 0, CollisionMask = CardLayer, Monitorable = false };
        _joinShape = new CollisionShape3D { Name = "Shape" };
        area.AddChild(_joinShape);
        area.BodyEntered += body =>
        {
            if (body is CardController card)
                _joining.Add(card);
        };
        AddChild(area);
    }
}
