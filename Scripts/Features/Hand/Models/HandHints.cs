using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>What the key hints of the hand depend on, read from the hand each frame.</summary>
/// <param name="Front">
///     The kind of the container in front, or null when a card is in front (or the hand is empty).
/// </param>
/// <param name="Count">How many items the hand holds.</param>
/// <param name="Cards">How many of them are cards.</param>
/// <param name="AimingDrop">True while the right button is held to aim a drop of the cards.</param>
/// <param name="LidOpen">True while the cardboard box in front has its flaps open.</param>
/// <param name="ItemsInside">How many items the open cardboard box in front still holds.</param>
/// <param name="BoosterFits">Whether the booster in front would open: its cards fit once it has left the hand.</param>
/// <param name="ShiftTakesMany">True when a Shift-click on what the player aims at would take many items.</param>
public readonly record struct HandHintState(
    CardContainerKind? Front,
    int Count,
    int Cards,
    bool AimingDrop = false,
    bool LidOpen = false,
    int ItemsInside = 0,
    bool BoosterFits = true,
    bool ShiftTakesMany = false);

/// <summary>
///     The key hints of the player's one hand: what the right button and Q do to the front item, the wheel once more
///     than one item is held, the strap key once two cards are held, and Shift-click when it would take many.
/// </summary>
public static class HandHints
{
    public static IReadOnlyList<ControlHint> For(HandHintState state)
    {
        if (state.AimingDrop && state.Cards > 0)
            return [new ControlHint("Release RMB", state.Cards == 1 ? "Set the card down" : "Set the cards down")];

        var hints = new List<ControlHint>();
        if (state.Count > 0)
            hints.AddRange(state.Front is { } kind ? ContainerHints(kind, state) : CardHints(state));
        if (state.Count > 1)
            hints.Add(new ControlHint("Wheel", "Bring the next item to the front"));
        if (state.Cards >= CardBundleRules.FewestToStrap)
            hints.Add(new ControlHint("V", "Strap the cards into a deck"));
        if (state.ShiftTakesMany)
            hints.Add(new ControlHint("Shift+LMB", "Take as many as fit"));
        return hints;
    }

    private static IEnumerable<ControlHint> CardHints(HandHintState state)
    {
        var cards = state.Cards == 1 ? "1 card" : $"{state.Cards} cards";
        yield return new ControlHint("RMB", $"Hold to aim, release to set down ({cards} in hand)");
        yield return new ControlHint("Q", "Put the card down");
        yield return new ControlHint("X", "Drop the front card");
    }

    private static IEnumerable<ControlHint> ContainerHints(CardContainerKind kind, HandHintState state)
    {
        var name = CardContainerKindText.NameOf(kind);
        yield return new ControlHint("RMB", kind switch
        {
            CardContainerKind.Box => $"{(state.LidOpen ? "Close" : "Open")} the {name}",
            CardContainerKind.Pack => $"Empty the {name} into your hand",
            _ when !state.BoosterFits => "No room in your hand for its cards",
            _ => $"Open the {name}",
        });
        yield return new ControlHint("Q", "Put it down");
        if (kind == CardContainerKind.Box && state.LidOpen)
            yield return new ControlHint("LMB",
                state.ItemsInside > 0 ? "Put one on the floor" : "Throw it in a trash can");
    }
}
