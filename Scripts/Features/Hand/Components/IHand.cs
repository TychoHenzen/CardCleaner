using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Hand.Components;

/// <summary>
///     The player's one hand: cards and containers in the order they were taken, with one front item that every hand
///     action applies to (see <see cref="Models.HandItems{T}" /> for the order and capacity rules). Held bodies hang
///     under the hand anchor (the player's camera), frozen and on no collision layer. As an <see cref="IItemStore" />
///     the hand peeks and takes its front item (a card, or a whole booster pack or booster box; a cardboard box is no
///     record, so the hand then peeks nothing), and a put builds the item's body and takes it in as the front.
/// </summary>
public interface IHand : IItemStore
{
    /// <summary>The group the player's hand joins, so a load can hand saved items back to it.</summary>
    const string GroupName = "hands";

    /// <summary>The most places a hand holds (<see cref="Models.HandItems{T}.MostPlaces" />).</summary>
    const int MostPlaces = Models.HandItems<Node3D>.MostPlaces;

    /// <summary>Every held body in hand order.</summary>
    IReadOnlyList<Node3D> Items { get; }

    /// <summary>The index of the front item in <see cref="Items" />, or -1 while the hand is empty.</summary>
    int FrontIndex { get; }

    /// <summary>The front item, or null while the hand is empty.</summary>
    Node3D? Front { get; }

    /// <summary>How many more one-place items (cards, booster packs) the hand takes now.</summary>
    int Room { get; }

    /// <summary>
    ///     How many items like <paramref name="item" /> the hand takes now: none when it refuses it, one for a booster
    ///     box or cardboard box (which fills the hand), else <see cref="Room" />.
    /// </summary>
    int RoomFor(StoredItem item);

    /// <summary>
    ///     Whether a booster in front would open now: the hand has room for its cards once the booster has left it.
    /// </summary>
    bool FrontBoosterFits { get; }

    /// <summary>The held cards in hand order.</summary>
    IReadOnlyList<RigidBody3D> Cards { get; }

    /// <summary>True while a booster opens in the hand; the hand then takes, gives and uses nothing.</summary>
    bool IsOpening { get; }

    /// <summary>The collision layer a card leaving the hand goes back on.</summary>
    uint CardCollisionLayer { get; }

    /// <summary>Whether <paramref name="body" /> (a card or a sealed container) may join the hand now.</summary>
    bool CanTake(Node3D body);

    /// <summary>
    ///     Takes a card or a sealed container into the hand, after the items held; false when it may not join.
    /// </summary>
    bool TryTake(Node3D body);

    /// <summary>
    ///     Gives a held body, frozen where it hangs, to a holder that locks it at once (a shelf slot): moved under the
    ///     world node, then given its collision back. False when the hand lacks it or no world node is registered.
    /// </summary>
    bool HandOver(Node3D body);

    /// <summary>Sets the front item down in front of the player (Q); the next item becomes the front.</summary>
    bool PutFrontDown();

    /// <summary>
    ///     Lets go of a held card where it hangs, so it falls (X on the front card, and a completed drop).
    /// </summary>
    bool ReleaseCard(RigidBody3D card);

    /// <summary>Moves the front by <paramref name="places" /> items, wrapping at either end (the wheel).</summary>
    void Rotate(int places);

    /// <summary>
    ///     Makes the item at <paramref name="index" /> the front; false when there is none (a save restore).
    /// </summary>
    bool SetFront(int index);

    /// <summary>
    ///     The right button on the front container: swings a cardboard box's flaps, empties a booster box into the hand,
    ///     or opens a booster whose cards fit once it has left. False when the front is a card or nothing happened.
    /// </summary>
    bool UseFront();

    /// <summary>
    ///     Takes the last item out of the held open cardboard box and sets it down where the player aims, within reach.
    ///     False when no open box with anything in it is held or nothing level is aimed at.
    /// </summary>
    bool PutOneDown();

    /// <summary>Throws the front item away: it leaves the hand and is freed. False when the hand is empty.</summary>
    bool DiscardFront();

    /// <summary>Lays the held items out: the fan with the front raised, or a carried box.</summary>
    void PositionItems();

    /// <summary>Lays the held cards flat in front of the player while a drop is aimed.</summary>
    void PositionCardsForDrop();

    /// <summary>
    ///     Where the item at <paramref name="index" /> lies in a hand of <paramref name="count" /> one-place items, in
    ///     the hand anchor's frame, raised when it is the front item.
    /// </summary>
    Transform3D PoseOf(int index, int count);

    /// <param name="handAnchor">The node held bodies hang under: the player's camera.</param>
    void SetReferences(Node3D handAnchor);
}
