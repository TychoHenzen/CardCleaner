using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Carries sealed containers in the player's hand: one cardboard box or booster box, or a stack of up to eight
///     booster packs. Picking up is refused for an opened container and for anything a full or boxed hand has no room
///     for; dropping returns the top one to the card spawner's node in front of the player.
/// </summary>
public interface IContainerHolder
{
    /// <summary>The top container in the hand, the one the other members act on; null while a booster opens.</summary>
    CardContainer? Held { get; }

    /// <summary>True while the hand holds a container or a booster is opening in it.</summary>
    bool IsHolding { get; }

    /// <summary>True while a held booster opens into the hand of cards (<see cref="UseHeld" />).</summary>
    bool IsOpening { get; }

    bool TryPickUp(CardContainer container);

    /// <summary>Sets the top held container down. False when nothing was released.</summary>
    bool Drop();

    /// <summary>
    ///     Opens or closes the held cardboard box's flaps; a held booster box empties into the hand as its eight booster
    ///     packs; the top held booster pack opens in front of the player and its cards go into the hand of cards. False
    ///     when nothing is held or nothing happened.
    /// </summary>
    bool UseHeld();

    /// <summary>
    ///     Takes the last item out of the held open cardboard box and sets it down on the floor or table the player
    ///     aims at, within reach. False when no open box with anything in it is held or nothing level is aimed at.
    /// </summary>
    bool PutOneDown();

    /// <summary>Throws the top held container away: it is freed. False when nothing is held.</summary>
    bool Discard();

    /// <param name="handAnchor">The node a held container hangs under: the player's camera.</param>
    /// <param name="cards">
    ///     The hand a booster opened in the hand gives its cards to; none means a booster stays shut.
    /// </param>
    void SetReferences(Node3D handAnchor, ICardHolder? cards = null);

    event ContainerHolder.ContainerPickedUpEventHandler ContainerPickedUp;
    event ContainerHolder.ContainerDroppedEventHandler ContainerDropped;
}
