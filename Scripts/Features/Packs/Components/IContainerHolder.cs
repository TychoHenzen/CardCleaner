using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Carries one sealed container (a box, pack or booster) in the player's hand. Picking up is refused for an
///     opened container; dropping returns it to the card spawner's node in front of the player.
/// </summary>
public interface IContainerHolder
{
    CardContainer? Held { get; }

    /// <summary>True while the hand holds a container or a booster is opening in it.</summary>
    bool IsHolding { get; }

    /// <summary>True while a held booster opens into the hand of cards (<see cref="UseHeld" />).</summary>
    bool IsOpening { get; }

    bool TryPickUp(CardContainer container);

    /// <summary>Sets the held container down. False when nothing was released.</summary>
    bool Drop();

    /// <summary>
    ///     Opens or closes the held box's flaps or pack's lid; a held booster opens in front of the player and its
    ///     cards go into the hand of cards. False when nothing is held or nothing happened.
    /// </summary>
    bool UseHeld();

    /// <param name="handAnchor">The node a held container hangs under: the player's camera.</param>
    /// <param name="cards">
    ///     The hand a booster opened in the hand gives its cards to; none means a booster stays shut.
    /// </param>
    void SetReferences(Node3D handAnchor, ICardHolder? cards = null);

    event ContainerHolder.ContainerPickedUpEventHandler ContainerPickedUp;
    event ContainerHolder.ContainerDroppedEventHandler ContainerDropped;
}
