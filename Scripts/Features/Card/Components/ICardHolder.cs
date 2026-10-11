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
    void SetReferences(Node3D handAnchor);

    event CardHolder.CardAddedEventHandler CardAdded;
    event CardHolder.CardRemovedEventHandler CardRemoved;
}