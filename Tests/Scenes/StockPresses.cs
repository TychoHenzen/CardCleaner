using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Orders items through the backoffice Stock app the way a player does (add the card, press Checkout) and asserts
/// what each order did, counting deliveries through the shared <see cref="OrderingService" />. One order is one unit
/// of one catalog item; a unit the checkout could not deliver is taken out of the cart again.
/// </summary>
internal sealed class StockPresses
{
    private const string DeliveryFullText = "The delivery area is full.";

    private readonly StockApp _app;
    private readonly OrderingService _ordering;
    private readonly Node _world;
    private readonly int _capacity;

    /// <param name="app">The Stock app whose cards are pressed.</param>
    /// <param name="ordering">The scene's ordering service, whose delivery count tells what a checkout spawned.</param>
    /// <param name="world">The node the delivered items are added to.</param>
    /// <param name="capacity">Slots in one delivery grid; it bounds <see cref="OrderUntilFull" />.</param>
    public StockPresses(StockApp app, OrderingService ordering, Node world, int capacity)
    {
        _app = app;
        _ordering = ordering;
        _world = world;
        _capacity = capacity;
    }

    /// <summary>Adds the card of item <paramref name="index" />, checks out and reports what happened.</summary>
    public PressResult Press(int index)
    {
        var id = _app.Catalog!.Items[index].Id;
        var before = _ordering.DeliveredCount;
        var children = _world.GetChildCount();

        _app.PressAdd(index);
        _app.PressCheckout();
        var delivered = _ordering.DeliveredCount - before;
        var spawned = delivered == 1 ? _world.GetChildren().LastOrDefault() as Node3D : null;
        var result = new PressResult(delivered, spawned, _app.MessageText, _world.GetChildCount() - children);
        // A refused unit stays in the cart; take it out so the next order starts from an empty cart.
        while (_app.CartLineCount > 0)
            _app.PressRemoveOne(id);
        return result;
    }

    /// <summary>Orders an item the app must deliver, and returns the node that order delivered.</summary>
    public Node3D PressForDelivery(int index) => CheckDelivery(index, Press(index));

    /// <summary>Orders an item the app must refuse with a message containing <paramref name="reason" />.</summary>
    public void PressForRefusal(int index, string reason)
    {
        var press = Press(index);

        AssertThat(press.Delivered)
            .OverrideFailureMessage($"refused order of item {index} delivered {press.Delivered} items")
            .IsEqual(0);
        AssertBool(press.Message.Contains(reason))
            .OverrideFailureMessage($"order of item {index} said \"{press.Message}\", expected \"{reason}\"")
            .IsTrue();
    }

    /// <summary>
    /// Orders an item until the app refuses one for lack of room, and every order before that must be a delivery.
    /// Returns the delivered items in order. A tall item claims more than one slot, so fewer than the grid's capacity
    /// of them fit.
    /// </summary>
    public List<Node3D> OrderUntilFull(int index)
    {
        var delivered = new List<Node3D>();
        // The bound only stops a grid that never fills from looping forever.
        for (var order = 0; order < 2 * _capacity; order++)
        {
            var press = Press(index);
            if (press.Delivered == 0)
            {
                AssertBool(press.Message.Contains(DeliveryFullText))
                    .OverrideFailureMessage($"the first refusal said \"{press.Message}\", expected a full area")
                    .IsTrue();
                return delivered;
            }

            delivered.Add(CheckDelivery(index, press));
        }

        AssertBool(false).OverrideFailureMessage("the grid never refused an order").IsTrue();
        return delivered;
    }

    /// <summary>Checks that an order delivered exactly one live item, and returns that node.</summary>
    private static Node3D CheckDelivery(int index, PressResult press)
    {
        AssertThat(press.Delivered)
            .OverrideFailureMessage($"order of item {index} delivered {press.Delivered} items (\"{press.Message}\")")
            .IsEqual(1);
        AssertThat(press.WorldChildren)
            .OverrideFailureMessage($"order of item {index} added {press.WorldChildren} nodes to the world")
            .IsEqual(1);

        var node = press.Spawned;
        AssertBool(node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            .OverrideFailureMessage($"order of item {index} delivered a node that is not live")
            .IsTrue();
        return node!;
    }

    /// <summary>What one order did: items delivered, the delivered node, the message, new world nodes.</summary>
    public readonly record struct PressResult(int Delivered, Node3D? Spawned, string Message, int WorldChildren);
}
