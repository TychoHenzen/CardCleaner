using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Opens a held booster in front of the player, timed by <see cref="BoosterRevealPlan" />: the booster comes to the
///     middle of the view, its strip tears off, its cards leave it one by one and spread into a fan, and the fan then
///     slides into the hand, where each card joins the cards already held. Everything happens in the frame of the hand
///     anchor (the camera) the booster hangs under, so the reveal follows the player's view and nothing collides.
/// </summary>
// ASSUMPTION (Medium): the hand's fan poses are in the same frame as the booster's, because the player's
// CardHighlighter gives the card holder and the container holder the same camera as their hand anchor.
internal sealed class BoosterReveal : IContainerRun
{
    private const float SmallestScale = 0.01f;

    // Half a booster's 0.045 m thickness: its origin is on its back face, and a card starts in its middle.
    private const float HalfThickness = 0.0225f;

    // Where the booster is held up to be torn open: low in the middle of the view, close enough to fill it.
    private static readonly Vector3 PresentAt = new(0f, -0.2f, -0.8f);

    private static readonly Transform3D Presented = new(CardFan.FacingCamera, PresentAt);
    private static readonly Transform3D CardStart = new(CardFan.FacingCamera, PresentAt + Vector3.Back * HalfThickness);

    // The fan the cards spread into above the torn booster, wider than the hand's so each card shows.
    private static readonly CardFan RevealFan = new(new Vector3(0f, 0.08f, -0.95f), 0.7f, 64f, 8, 0.006f);

    private readonly CardContainer _wrapper;
    private readonly Node3D _anchor;
    private readonly ICardHolder _hand;
    private readonly IReadOnlyList<CardSignature> _signatures;
    private readonly ICardSpawningService _spawning;
    private readonly BoosterRevealPlan _plan;
    private readonly List<OpeningPart> _parts;
    private readonly Node3D? _visual;
    private readonly Transform3D _from;
    private readonly List<RigidBody3D> _cards = new();
    private readonly List<Slide> _slides = new();
    private float _time;
    private float _intoHandAt = -1f;
    private bool _given;

    public BoosterReveal(CardContainer wrapper, Node3D anchor, ICardHolder hand,
        IReadOnlyList<CardSignature> signatures, ICardSpawningService spawning)
    {
        _wrapper = wrapper;
        _anchor = anchor;
        _hand = hand;
        _signatures = signatures;
        _spawning = spawning;
        _plan = new BoosterRevealPlan(signatures.Count);
        _from = wrapper.Transform;
        _visual = wrapper.GetNodeOrNull<Node3D>("Visual");
        _parts = _visual != null ? OpeningPart.Under(_visual) : new List<OpeningPart>();
    }

    public int Released { get; private set; }

    public int Pending => _signatures.Count - Released;

    public bool IsOver => _given;

    public bool Advance(float delta)
    {
        if (_given)
            return true;

        _time += delta;
        PoseWrapper();
        if (Released < _signatures.Count && _time >= _plan.ReleaseAt(Released))
            Release();

        if (_intoHandAt < 0f)
            Spread();
        else
            SlideIntoHand();

        if (Released == _signatures.Count && _intoHandAt < 0f && _time >= _plan.IntoHandStart)
            StartSlide();

        return _given;
    }

    /// <summary>Every card still inside comes out and the whole booster's cards join the hand at once.</summary>
    public void Finish()
    {
        if (_given)
            return;

        while (Released < _signatures.Count)
            Release();

        _time = _plan.Duration;
        PoseWrapper();
        GiveToHand();
    }

    // The booster left the tree (a load cleared it, or the scene closed) with cards out of it. They hang under the
    // anchor, not the booster, so they are handed to the hand once the tree has settled, if they are still there.
    public void Abandon()
    {
        if (_given)
            return;

        _given = true;
        var cards = _cards.ToArray();
        Callable.From(() =>
        {
            foreach (var card in cards)
                if (StillOurs(card) && card.IsInsideTree())
                    _hand.AddCard(card);
        }).CallDeferred();
    }

    private void PoseWrapper()
    {
        if (!GodotObject.IsInstanceValid(_wrapper))
            return;

        _wrapper.Transform = _from.InterpolateWith(Presented, ContainerOpeningPlan.Ease(_plan.PresentProgress(_time)));
        var tear = _plan.TearProgress(_time);
        foreach (var part in _parts)
            part.Pose(tear);

        if (_visual != null)
            _visual.Scale = Vector3.One *
                            Mathf.Max(SmallestScale, 1f - ContainerOpeningPlan.Ease(_plan.VanishProgress(_time)));
    }

    // Spawns the next card inside the booster, frozen, with its collision off and on no layer, so it never meets the
    // world while it hangs in front of the camera and no slot takes it (DeckSlot.IsFreeStock). At most one card a
    // frame, so a slow frame never spawns the whole booster at once.
    private void Release()
    {
        var signature = _signatures[Released++];
        if (!GodotObject.IsInstanceValid(_anchor)
            || _spawning.SpawnCard(signature, _anchor.GlobalTransform * CardStart, _anchor) is not RigidBody3D card)
            return;

        card.Freeze = true;
        if (card.GetNodeOrNull<CollisionShape3D>("CardCollision") is { } shape)
            shape.Disabled = true;
        card.CollisionLayer = 0;
        card.Transform = CardStart;
        _cards.Add(card);
    }

    private void Spread()
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            if (!StillOurs(_cards[i]))
                continue;

            var rise = ContainerOpeningPlan.Ease(_plan.RiseProgress(i, _time));
            _cards[i].Transform = CardStart.InterpolateWith(RevealFan.Pose(i, _signatures.Count), rise);
        }
    }

    // Each card, held and revealed alike, slides from where it is to its place in the hand it is about to become.
    private void StartSlide()
    {
        _intoHandAt = _time;
        var held = _hand.HeldCards;
        var total = held.Length + _cards.Count;
        for (var j = 0; j < held.Length; j++)
            _slides.Add(new Slide(held[j], held[j].Transform, _hand.FanPose(j, total)));
        for (var k = 0; k < _cards.Count; k++)
            _slides.Add(new Slide(_cards[k], _cards[k].Transform, _hand.FanPose(held.Length + k, total)));
    }

    private void SlideIntoHand()
    {
        var progress = BoosterRevealPlan.IntoHandProgress(_time - _intoHandAt);
        foreach (var slide in _slides)
            if (StillOurs(slide.Card))
                slide.Card.Transform = slide.From.InterpolateWith(slide.To, ContainerOpeningPlan.Ease(progress));

        if (progress >= 1f)
            GiveToHand();
    }

    // The hand takes the cards in the order they left the booster, so the first card out ends up leftmost.
    private void GiveToHand()
    {
        _given = true;
        foreach (var card in _cards)
            if (StillOurs(card))
                _hand.AddCard(card);
    }

    // A card is still the reveal's (or the hand's) to move while it lives and hangs under the anchor; one a load freed
    // or the player dropped is left alone.
    private bool StillOurs(RigidBody3D card) =>
        GodotObject.IsInstanceValid(card) && GodotObject.IsInstanceValid(_anchor) && card.GetParent() == _anchor;

    private readonly record struct Slide(RigidBody3D Card, Transform3D From, Transform3D To);
}
