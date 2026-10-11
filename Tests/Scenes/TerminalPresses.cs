using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Presses items on an open order terminal the way a player does and asserts what each press did, counting
/// deliveries through the shared <see cref="OrderingService" />.
/// </summary>
internal sealed class TerminalPresses
{
    private readonly OrderingService _ordering;
    private readonly int _capacity;

    /// <param name="ordering">
    /// The scene's ordering service, whose delivery count tells how many items a press spawned.
    /// </param>
    /// <param name="capacity">Slots in one delivery grid; it bounds <see cref="OrderUntilFull" />.</param>
    public TerminalPresses(OrderingService ordering, int capacity)
    {
        _ordering = ordering;
        _capacity = capacity;
    }

    /// <summary>
    /// Presses an item and reports what it did. The message is cleared first: LastOrder is reset only inside the
    /// terminal's order handler, so a press that never arrives would otherwise leave the previous result in place.
    /// </summary>
    public PressResult Press(OrderTerminal terminal, int index)
    {
        var ui = terminal.Ui!;
        var before = _ordering.DeliveredCount;
        ui.ShowMessage(string.Empty);
        ui.PressItem(index);

        AssertBool(!string.IsNullOrEmpty(ui.MessageText))
            .OverrideFailureMessage($"press of item {index} never reached the terminal")
            .IsTrue();
        return new PressResult(terminal.LastOrder, _ordering.DeliveredCount - before);
    }

    /// <summary>Presses an item the terminal must deliver, and returns the node that press delivered.</summary>
    public Node3D PressForDelivery(OrderTerminal terminal, int index) => CheckDelivery(index, Press(terminal, index));

    /// <summary>
    /// Presses an item the terminal must refuse with <paramref name="status"/>, and nothing is delivered.
    /// </summary>
    public void PressForRefusal(OrderTerminal terminal, int index, OrderStatus status)
    {
        var press = Press(terminal, index);

        AssertThat(press.Delivered)
            .OverrideFailureMessage($"refused press of item {index} delivered {press.Delivered} items")
            .IsEqual(0);
        AssertBool(press.Order?.Status == status)
            .OverrideFailureMessage($"press of item {index} gave {press.Order?.Status}, expected {status}")
            .IsTrue();
    }

    /// <summary>
    /// Orders an item until the terminal refuses one for lack of room, and every order before that must be a delivery.
    /// Returns the delivered items in order. A tall item claims more than one slot, so fewer than the grid's capacity
    /// of them fit.
    /// </summary>
    public List<Node3D> OrderUntilFull(OrderTerminal terminal, int index)
    {
        var delivered = new List<Node3D>();
        // The bound only stops a grid that never fills from looping forever.
        for (var order = 0; order < 2 * _capacity; order++)
        {
            var press = Press(terminal, index);
            if (press.Delivered == 0)
            {
                AssertBool(press.Order?.Status == OrderStatus.DeliveryFull)
                    .OverrideFailureMessage($"the first refusal was {press.Order?.Status}, expected DeliveryFull")
                    .IsTrue();
                return delivered;
            }

            delivered.Add(CheckDelivery(index, press));
        }

        AssertBool(false).OverrideFailureMessage("the grid never refused an order").IsTrue();
        return delivered;
    }

    /// <summary>Checks that a press delivered exactly one live item, and returns that node.</summary>
    private static Node3D CheckDelivery(int index, PressResult press)
    {
        AssertThat(press.Delivered)
            .OverrideFailureMessage($"press of item {index} delivered {press.Delivered} items ({press.Order?.Status})")
            .IsEqual(1);
        AssertBool(press.Order is { Status: OrderStatus.Success, Spawned: not null })
            .OverrideFailureMessage($"press of item {index} was not a delivery: {press.Order?.Status}")
            .IsTrue();

        var node = press.Order!.Value.Spawned!;
        AssertBool(GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            .OverrideFailureMessage($"press of item {index} delivered a node that is not live")
            .IsTrue();
        return node;
    }

    /// <summary>What one press did: the terminal's latest result, and how many items ordering delivered.</summary>
    public readonly record struct PressResult(OrderResult? Order, int Delivered);
}
