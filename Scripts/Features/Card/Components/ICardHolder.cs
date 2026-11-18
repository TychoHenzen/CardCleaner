using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public interface ICardHolder
{
    bool HasCards { get; }
    int HeldCount { get; }
    RigidBody3D[] HeldCards { get; }

    void AddCard(RigidBody3D card);
    void RemoveCard(RigidBody3D card);
    void RemoveTopCard();
    void RemoveAllCards();
    void PositionCards();
    void PositionCardsForDrop();

    /// <summary>
    ///     Where the card at <paramref name="index" /> lies in a hand of <paramref name="count" /> cards, in the
    ///     frame of the hand anchor the cards hang under.
    /// </summary>
    Transform3D FanPose(int index, int count);
    void SetReferences(Node3D handAnchor);

    event CardHolder.CardAddedEventHandler CardAdded;
    event CardHolder.CardRemovedEventHandler CardRemoved;
}