using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     The look of a pile: a cream mound that follows the count, with its newest <see cref="FacesShown" /> cards lying
///     face up on top, each at its <see cref="PileLayout" /> pose. Each shown card is a <see cref="CardFace" />, the
///     same face a loose card shows, dressed from the card's signature and look seed, so a card looks the same in the
///     pile as out of it. Each face is kept while its card stays among the newest, so a join adds one face and a take
///     frees one, and no other card moves or is dressed again. Every part collides with nothing.
/// </summary>
public partial class PileLook : Node3D, IDependent
{
    /// <summary>
    ///     A pile draws at most its newest this many cards; older ones lie under them, inside the mound.
    /// </summary>
    // Measured with Tests/Features/CardStacks/Probe/PileFrameCostProbe.tscn through tools/e2e (vsync off): an empty
    // view took 0.59 ms a frame, one pile of 100, 500 or 2000 cards 1.2 to 1.4 ms (GPU 0.20 to 0.29 ms), 4 piles
    // 3.7 ms and 9 piles 6.2 ms. A pile costs about 0.65 ms whatever its size, since it never draws more than this
    // many faces. ASSUMPTION (Medium): a shop holds only a few piles, since a pile forms only from over 100 loose
    // cards; nothing caps the count, and 9 piles already take over a third of a 60 fps frame on this machine.
    public const int FacesShown = 64;

    // The mound sits a little inside the dome the cards lie on, so it never shows through them.
    private const float CoreShare = 0.95f;

    private static readonly Lazy<PackedScene> FaceScene = new(() => GD.Load<PackedScene>(CardFace.ScenePath));

    private readonly SphereMesh _moundMesh = new() { IsHemisphere = true };
    private readonly Dictionary<int, CardFace> _shown = new();

    // The card each shown face is waiting to be dressed as, until the generator is resolved.
    private readonly Dictionary<CardFace, StoredCard> _undressed = new();

    private IServiceResolver? _services;
    private ICardGenerator? _generator;

    public PileLook()
    {
        AddChild(new MeshInstance3D
        {
            Name = "Mound",
            Mesh = _moundMesh,
            MaterialOverride = CardBlockLook.EdgeMaterial(),
        });
    }

    /// <summary>The face drawn for each shown card, by its index in join order.</summary>
    public IReadOnlyDictionary<int, CardFace> Shown => _shown;

    public void Inject(IServiceResolver services) => _services = services;

    // A pile is built before it enters the tree, so its first faces are dressed once the generator is resolved.
    public override void _Ready() => _services?.Get<ICardGenerator>(generator =>
    {
        // The pile may have been taken empty and freed before the generator was registered.
        if (!IsInstanceValid(this))
            return;

        _generator = generator;
        foreach (var (face, card) in _undressed.ToList())
            Dress(face, card);
    });

    /// <summary>
    ///     Shows <paramref name="cards" /> (in join order) laid with <paramref name="seed" />: frees the faces of
    ///     cards that left or dropped out of the newest <see cref="FacesShown" />, and adds one for each card that
    ///     joined them.
    /// </summary>
    public void Show(IReadOnlyList<StoredCard> cards, int seed)
    {
        var count = cards.Count;
        _moundMesh.Radius = CardBundleRules.PileRadius(count) * CoreShare;
        _moundMesh.Height = CardBundleRules.PileHeight(count) * CoreShare;

        var first = Math.Max(0, count - FacesShown);
        foreach (var gone in _shown.Keys.Where(index => index < first || index >= count).ToList())
        {
            // Nothing else holds a shown face: freed at once, so no old card waits outside the tree.
            _undressed.Remove(_shown[gone]);
            _shown[gone].Free();
            _shown.Remove(gone);
        }

        for (var index = first; index < count; index++)
        {
            if (_shown.ContainsKey(index))
                continue;

            var face = FaceScene.Value.Instantiate<CardFace>();
            face.Name = $"Card{index}";
            face.Transform = PileLayout.PoseOf(seed, index);
            AddChild(face);
            _shown[index] = face;
            Dress(face, cards[index]);
        }
    }

    // A record without a signature is a mundane card, as a body made from it would be (StoredItemBodies).
    private void Dress(CardFace face, StoredCard card)
    {
        if (_generator == null)
        {
            _undressed[face] = card;
            return;
        }

        _undressed.Remove(face);
        face.Dress(_generator, card.Signature ?? new CardSignature(), card.LookSeed);
    }
}
