using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Hand.Components;

/// <summary>
///     What the right button does to the hand's front container (<see cref="Hand.UseFront" />), and the booster that
///     is opening in the hand meanwhile.
/// </summary>
internal sealed class HandContainerUse(Hand hand)
{
    private CardContainer? _opening;

    /// <summary>The booster opening in the hand, or null once its cards are in the hand or it was freed.</summary>
    public CardContainer? Opening
    {
        get
        {
            if (_opening != null && (!GodotObject.IsInstanceValid(_opening) || !_opening.IsOpeningInHand))
                _opening = null;
            return _opening;
        }
    }

    /// <summary>Swings a cardboard box's flaps, empties a booster box into the hand, or opens a booster.</summary>
    public bool Use(CardContainer container) =>
        container.Kind switch
        {
            CardContainerKind.Box => container.SetLidOpen(!container.IsLidOpen),
            CardContainerKind.Pack => EmptyIntoHand(container),
            _ => OpenInHand(container)
        };

    // The booster box leaves the hand, used up, and its booster packs take its place, held as picked-up ones are.
    private bool EmptyIntoHand(CardContainer pack)
    {
        var boosters = pack.EmptyIntoHand();
        hand.PositionItems();
        foreach (var booster in boosters)
            hand.TryTake(booster);

        return boosters.Count > 0;
    }

    // A booster's cards all go into the hand, so it opens only while they fit once the booster has left it. Opening
    // marks the booster opened, so the hand lets go of it, and the hand stays busy through Opening.
    private bool OpenInHand(CardContainer booster)
    {
        if (!hand.FrontBoosterFits || !booster.OpenInHand(hand))
            return false;

        _opening = booster;
        hand.PositionItems();
        return true;
    }
}
