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
