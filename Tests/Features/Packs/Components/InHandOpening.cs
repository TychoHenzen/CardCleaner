using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     Opens containers the two ways the player's hand does, without a holder: a booster box empties into the hand
///     (<see cref="CardContainer.EmptyIntoHand" />) and a booster opens into a hand of cards
///     (<see cref="CardContainer.OpenInHand" />), ended at once with <see cref="CardContainer.FinishOpening" />.
/// </summary>
public static class InHandOpening
{
    /// <summary>
    ///     A camera under <paramref name="parent" /> to hold containers: a container under a camera is held.
    /// </summary>
    public static Camera3D AddHand(Node parent)
    {
        var hand = new Camera3D { Name = "Hand" };
        parent.AddChild(hand);
        return hand;
    }

    /// <summary>
    ///     Empties <paramref name="pack" /> into <paramref name="hand" /> and returns its eight boosters in order.
    /// </summary>
    public static IReadOnlyList<CardContainer> Empty(CardContainer pack, Camera3D hand)
    {
        Hold(pack, hand);
        var boosters = pack.EmptyIntoHand();
        AssertThat(boosters.Count).IsEqual(CardContainerLayout.ItemsPerContainer);
        return boosters;
    }

    /// <summary>
    ///     Opens <paramref name="booster" /> in <paramref name="hand" /> and ends the reveal at once, so every card is
    ///     spawned, in the order the booster gives them. The cards go to <paramref name="cards" />, or to a stand-in
    ///     hand when it is null.
    /// </summary>
    public static void Open(CardContainer booster, Camera3D hand, ICardHolder? cards = null)
    {
        Begin(booster, hand, cards);
        booster.FinishOpening();
    }

    /// <summary>
    ///     Starts opening <paramref name="booster" /> in <paramref name="hand" /> and leaves the reveal running.
    /// </summary>
    public static void Begin(CardContainer booster, Camera3D hand, ICardHolder? cards = null)
    {
        Hold(booster, hand);
        AssertBool(booster.OpenInHand(cards ?? Substitute.For<ICardHolder>())).IsTrue();
    }

    /// <summary>Puts <paramref name="container" /> under <paramref name="hand" />, which makes it held.</summary>
    public static void Hold(CardContainer container, Camera3D hand)
    {
        if (container.GetParent() == hand)
            return;

        if (container.GetParent() is { } parent)
            parent.RemoveChild(container);
        hand.AddChild(container);
    }
}
