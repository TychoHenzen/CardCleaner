using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     Heaps loose cards into piles (<see cref="CardBundle" />) under its parent, every <see cref="GatherSeconds" />.
///     A loose card is one at rest in the world: frozen (a card freezes once it lies still), on a collision layer, and
///     held by no hand, slot or booster reveal. More than <see cref="CardBundleRules.MostLooseCards" /> loose cards
///     linked within <see cref="LinkDistance" /> of each other become one pile where they lay; a loose card that comes
///     to rest on or beside a pile joins it; and two piles that run into each other become one. The shop puts it
///     directly under its scene root, which is where a save load puts the cards, decks and piles it restores.
/// </summary>
public partial class CardPileGatherer : Node
{
    private const float DefaultGatherSeconds = 0.5f;
    private const float DefaultLinkDistance = 0.5f;

    // A card resting this far below a pile's base or above its top still lies on it.
    private const float JoinBelow = 0.1f;
    private const float JoinAbove = 0.3f;

    // Two piles merge once they overlap by this share of their summed radii.
    private const float MergeShare = 0.75f;

    // A new pile starts a hair above the floor the cards lay on, so it never starts inside it.
    private const float StartLift = 0.005f;

    /// <summary>How often the loose cards are gathered.</summary>
    [Export] public float GatherSeconds = DefaultGatherSeconds;

    /// <summary>Cards closer together than this lie in close proximity.</summary>
    [Export] public float LinkDistance = DefaultLinkDistance;

    private double _sinceGather;

    public override bool _PropertyCanRevert(StringName property) =>
        property.ToString() is nameof(GatherSeconds) or nameof(LinkDistance) || base._PropertyCanRevert(property);

    public override Variant _PropertyGetRevert(StringName property) => property.ToString() switch
    {
        nameof(GatherSeconds) => DefaultGatherSeconds,
        nameof(LinkDistance) => DefaultLinkDistance,
        _ => base._PropertyGetRevert(property)
    };

    public override void _PhysicsProcess(double delta)
    {
        _sinceGather += delta;
        if (_sinceGather < GatherSeconds)
            return;

        _sinceGather = 0;
        Gather();
    }

    /// <summary>One pass: loose cards join the piles they lie on, form new piles, then touching piles merge.</summary>
    public void Gather()
    {
        if (GetParent() is not { } scope)
            return;

        var piles = GetTree().GetNodesInGroup(CardBundle.GroupName).OfType<CardBundle>()
            .Where(bundle => bundle.Kind == CardBundleKind.Pile && IsAlive(bundle) && scope.IsAncestorOf(bundle))
            .ToList();
        var loose = GetTree().GetNodesInGroup("Cards").OfType<CardController>()
            .Where(card => scope.IsAncestorOf(card) && IsLoose(card)).ToList();

        JoinPiles(loose, piles);
        FormPiles(loose, piles);
        MergePiles(piles);
    }

    private static bool IsAlive(Node node) => node.IsInsideTree() && !node.IsQueuedForDeletion();

    private static bool IsLoose(CardController card) =>
        IsAlive(card) && card.Freeze && card.CollisionLayer != 0 && !HasAncestor<DeckSlot>(card)
        && !HasAncestor<Camera3D>(card);

    private static bool HasAncestor<T>(Node node)
    {
        for (var parent = node.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is T)
                return true;

        return false;
    }

    // Each card that lies on or beside a pile goes into the nearest such pile; the joined cards leave the list.
    private void JoinPiles(List<CardController> loose, List<CardBundle> piles)
    {
        if (piles.Count == 0)
            return;

        var joining = piles.ToDictionary(pile => pile, _ => new List<CardController>());
        foreach (var card in loose)
        {
            if (piles.Where(pile => LiesOn(card.GlobalPosition, pile))
                    .OrderBy(pile => Flat(card.GlobalPosition - pile.GlobalPosition).Length())
                    .FirstOrDefault() is { } pile)
                joining[pile].Add(card);
        }

        foreach (var (pile, cards) in joining.Where(entry => entry.Value.Count > 0))
        {
            pile.Add(Take(cards));
            loose.RemoveAll(cards.Contains);
        }
    }

    private bool LiesOn(Vector3 point, CardBundle pile)
    {
        var reach = CardBundleRules.PileRadius(pile.Count) + LinkDistance / 2f;
        var above = point.Y - pile.GlobalPosition.Y;
        return Flat(point - pile.GlobalPosition).Length() <= reach
               && above >= -JoinBelow && above <= CardBundleRules.PileHeight(pile.Count) + JoinAbove;
    }

    private void FormPiles(List<CardController> loose, List<CardBundle> piles)
    {
        if (loose.Count <= CardBundleRules.MostLooseCards)
            return;

        var points = loose.Select(card => card.GlobalPosition).ToList();
        foreach (var group in CardClusters.LargerThan(points, LinkDistance, CardBundleRules.MostLooseCards))
        {
            var cards = group.Select(index => loose[index]).ToList();
            if (cards[0].GetParent() is not Node3D parent)
                continue;

            var centre = cards.Aggregate(Vector3.Zero, (sum, card) => sum + card.GlobalPosition) / cards.Count;
            var floor = cards.Min(card => card.GlobalPosition.Y) - CardBundleRules.CardSize.Y / 2f;
            var pile = CardBundle.Create(CardBundleKind.Pile, Take(cards));
            parent.AddChild(pile);
            pile.GlobalPosition = new Vector3(centre.X, floor + StartLift, centre.Z);
            piles.Add(pile);
        }
    }

    // A later pile pours into an earlier one it runs into, so the pile that formed first keeps its place.
    private static void MergePiles(List<CardBundle> piles)
    {
        for (var i = 0; i < piles.Count; i++)
        for (var j = piles.Count - 1; j > i; j--)
        {
            if (!Touch(piles[i], piles[j]))
                continue;

            piles[i].Add(piles[j].Take(piles[j].Count));
            piles.RemoveAt(j);
        }
    }

    private static bool Touch(CardBundle a, CardBundle b)
    {
        var reach = (CardBundleRules.PileRadius(a.Count) + CardBundleRules.PileRadius(b.Count)) * MergeShare;
        var height = Mathf.Max(CardBundleRules.PileHeight(a.Count), CardBundleRules.PileHeight(b.Count));
        return Flat(a.GlobalPosition - b.GlobalPosition).Length() <= reach
               && Mathf.Abs(a.GlobalPosition.Y - b.GlobalPosition.Y) <= height;
    }

    // The cards become records and leave the tree at once, so no later pass finds them again.
    private static List<StoredCard> Take(List<CardController> cards)
    {
        var records = cards.Select(card => (StoredCard)StoredItemBodies.RecordOf(card)).ToList();
        foreach (var card in cards)
        {
            card.GetParent()?.RemoveChild(card);
            card.QueueFree();
        }

        return records;
    }

    private static Vector2 Flat(Vector3 offset) => new(offset.X, offset.Z);
}
