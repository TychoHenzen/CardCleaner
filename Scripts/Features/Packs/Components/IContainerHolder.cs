using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Carries one sealed container (a box, pack or booster) in the player's hand. Picking up is refused for an
///     opened container; dropping returns it to the card spawner's node in front of the player.
/// </summary>
public interface IContainerHolder
{
    CardContainer? Held { get; }
    bool IsHolding { get; }

    bool TryPickUp(CardContainer container);
    void Drop();
    bool OpenHeld();
    void SetReferences(Node3D handAnchor);

    event ContainerHolder.ContainerPickedUpEventHandler ContainerPickedUp;
    event ContainerHolder.ContainerDroppedEventHandler ContainerDropped;
}
