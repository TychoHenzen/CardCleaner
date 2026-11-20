using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     One board of a shelf: the unit the player stocks, prices and takes from. Its <see cref="DeckSlot" /> children
///     are seats that only lock and pose bodies; the level decides. It holds one kind of stock at a time (<see
///     cref="ShelfPlacementRule.LevelTakes" />) and fills its seats in order, so a body put anywhere on the board goes
///     into the next free seat, and a take removes the last one put in. When a body leaves from the middle (a card
///     picked up by hand, a load clearing the shelf) the later stock closes up, so the seats stay filled in order. On a
///     product shelf every item on the level asks one price (<see cref="ShelfPlacementRule.PriceOnPut" />). As an <see
///     cref="IItemStore" /> it moves cards, booster packs and booster boxes as records; a cardboard box is no record,
///     so it goes on and off a rack level as a body (<see cref="TryLock" />).
/// </summary>
public partial class ShelfLevel : Node3D, IItemStore, IDependent
{
    [Signal]
    public delegate void ChangedEventHandler();

    private IServiceResolver? _services;
    private int _count;
    private bool _closeUpQueued;
    private bool _closingUp;

    /// <summary>The shelf kind this level belongs to, which decides the stock it takes. Set by its shelf.</summary>
    public ShelfKind Kind { get; private set; } = ShelfKind.CardShelf;

    /// <summary>The seats of this level in fill order (scene order).</summary>
    public IReadOnlyList<DeckSlot> Seats => GetChildren().OfType<DeckSlot>().ToList();

    /// <summary>
    ///     Every body on the level in the order it was put on: seat by seat, each seat's stack bottom first.
    /// </summary>
    public IReadOnlyList<RigidBody3D> Stock => Seats.SelectMany(seat => seat.Cards).ToList();

    public int Count => Seats.Sum(seat => seat.Cards.Count);

    /// <summary>The stock this level holds, or null while it is empty.</summary>
    public StockKind? Holds => Seats.Select(seat => seat.Holds).FirstOrDefault(held => held != null);

    /// <summary>
    ///     The body put on last, which a take removes and a customer buys next; null while the level is empty.
    /// </summary>
    public RigidBody3D? Last => Seats.LastOrDefault(seat => seat.HasCards)?.Cards[^1];

    /// <summary>
    ///     True while the level takes one more of what it holds, or anything its shelf takes while empty.
    /// </summary>
    public bool HasRoom => Holds is { } held
        ? NextSeatFor(held) != null
        : Seats.Any(seat => seat.Capacity > 0);

    /// <summary>How many seats hold nothing.</summary>
    public int EmptySeats => Seats.Count(seat => !seat.HasCards);

    /// <summary>
    ///     Sets the shelf kind on the level and its seats, and takes over their triggers. Called by the shelf.
    /// </summary>
    public void Configure(ShelfKind kind)
    {
        Kind = kind;
        foreach (var seat in Seats)
        {
            seat.Kind = kind;
            // The level decides where a body landing on the board goes, so a seat no longer locks what enters it.
            seat.LocksOnEntry = false;
        }
    }

    public void Inject(IServiceResolver services) => _services = services;

    // Subscribed on every entry, not in _Ready, so a level whose shelf is reparented keeps hearing its seats.
    public override void _EnterTree()
    {
        foreach (var seat in Seats)
        {
            seat.CardsChanged -= OnSeatChanged;
            seat.CardsChanged += OnSeatChanged;
            if (seat.Area is not { } area)
                continue;
            area.BodyEntered -= OnBodyEntered;
            area.BodyEntered += OnBodyEntered;
        }

        _count = Count;
    }

    public override void _ExitTree()
    {
        foreach (var seat in Seats)
        {
            seat.CardsChanged -= OnSeatChanged;
            if (seat.Area != null)
                seat.Area.BodyEntered -= OnBodyEntered;
        }
    }

    /// <summary>
    ///     The seat the next <paramref name="stock" /> goes into, or null when the level does not take it now.
    /// </summary>
    public DeckSlot? NextSeatFor(StockKind stock) =>
        ShelfPlacementRule.LevelTakes(Kind, Holds, stock) ? Seats.FirstOrDefault(seat => seat.CanTake(stock)) : null;

    /// <summary>Whether the level takes one more <paramref name="stock" /> now.</summary>
    public bool CanTake(StockKind stock) => NextSeatFor(stock) != null;

    /// <summary>
    ///     Whether <see cref="TryLock" /> would take <paramref name="body" /> now: free stock the level takes.
    /// </summary>
    public bool CanTake(RigidBody3D body) =>
        IsVisibleInTree() && DeckSlot.IsFreeStock(body) && DeckSlot.StockOf(body) is { } stock && CanTake(stock);

    /// <summary>
    ///     Locks <paramref name="body" /> into the level's next free seat, giving it the level's price on a product
    ///     shelf. False, with the body left alone, when the level does not take it.
    /// </summary>
    public bool TryLock(RigidBody3D body)
    {
        if (!CanTake(body) || NextSeatFor(DeckSlot.StockOf(body)!.Value) is not { } seat)
            return false;

        var price = ShelfPlacementRule.PriceOnPut(Kind, Count == 0, LevelPrice(), PriceOf(body));
        if (!seat.TryLock(body))
            return false;

        SetPrice(body, price);
        return true;
    }

    /// <summary>
    ///     The part of the level <paramref name="point" /> (in world space) lies over: its stock when the seat nearest
    ///     it holds anything, else free space.
    /// </summary>
    public AimPart PartAt(Vector3 point)
    {
        var nearest = Seats.OrderBy(seat => Flat(seat.SeatPosition(0) - point).LengthSquared()).FirstOrDefault();
        return nearest is { HasCards: true } ? AimPart.Stock : AimPart.Space;
    }

    // The price every item on the level asks: the first item's own, which every later one took.
    private int? LevelPrice() => Stock is [var first, ..] ? PriceOf(first) : null;

    public StoredItem? Peek() =>
        Last is { } last && StoredItemBodies.ShapeOf(last) != null ? StoredItemBodies.CurrentRecordOf(last) : null;

    public bool CanPut(StoredItem item) => IsVisibleInTree() && CanTake(ShelfPlacementRule.From(item));

    public bool Put(StoredItem item)
    {
        if (!CanPut(item) || NextSeatFor(ShelfPlacementRule.From(item)) is not { } seat)
            return false;

        var price = ShelfPlacementRule.PriceOnPut(Kind, Count == 0, LevelPrice(), PriceOf(item));
        var at = new Transform3D(seat.GlobalBasis, seat.SeatPosition(seat.Cards.Count));
        if (StoredItemBodies.Build(Priced(item, price), seat, at, Spawning()) is not { } body)
            return false;
        if (seat.TryLock(body))
            return true;

        // Out of the tree at once, before any physics step can report it entering a seat's trigger.
        seat.RemoveChild(body);
        body.QueueFree();
        return false;
    }

    // The last body leaves as its record (a container without a seed draws one now) and is freed; its price goes
    // with it.
    public StoredItem? TakeLast()
    {
        if (Last is not { } last || StoredItemBodies.ShapeOf(last) == null || last.GetParent() is not DeckSlot seat)
            return null;

        var record = StoredItemBodies.RecordOf(last);
        seat.ReleaseCard(last);
        seat.RemoveChild(last);
        last.QueueFree();
        return record;
    }

    // A body landing anywhere on the board goes into the next free seat. One the level does not take now (no room, or
    // another kind than it holds) is pushed off; one its shelf never takes is left alone, as a seat leaves it.
    private void OnBodyEntered(Node3D body)
    {
        if (body is not RigidBody3D rigid || DeckSlot.StockOf(rigid) is not { } stock
            || !ShelfPlacementRule.Accepts(Kind, stock) || !DeckSlot.IsFreeStock(rigid))
            return;

        if (!CanTake(stock))
        {
            if (Seats is [var first, ..])
                first.Eject(rigid);
            return;
        }

        // Deferred: the body may enter several seats' triggers in one step, and TryLock checks again that it is still
        // free, so only the first call locks it.
        CallDeferred(MethodName.LockEntered, rigid);
    }

    private void LockEntered(RigidBody3D body)
    {
        if (IsInstanceValid(body))
            TryLock(body);
    }

    private void OnSeatChanged()
    {
        var count = Count;
        // Only a body leaving opens a gap. A lock never closes up: a load locks saved stock seat by seat, possibly out
        // of order, and moving it would undo the saved seats.
        if (count < _count && !_closingUp && !_closeUpQueued)
        {
            _closeUpQueued = true;
            // Deferred: a seat may be in the middle of removing a child (a load frees every body at once), and a
            // reparent into a busy node fails.
            CallDeferred(MethodName.CloseUp);
        }

        _count = count;
        if (!_closingUp)
            EmitSignal(SignalName.Changed);
    }

    // Moves the stock down into the seats a gap left free, keeping its order, so the last body put on stays last.
    private void CloseUp()
    {
        _closeUpQueued = false;
        if (!IsInsideTree() || IsFilledInOrder())
            return;

        _closingUp = true;
        var stock = Stock.Where(body => IsInstanceValid(body) && !body.IsQueuedForDeletion()).ToList();
        foreach (var body in stock)
            (body.GetParent() as DeckSlot)?.ReleaseCard(body);
        foreach (var body in stock)
        {
            if (Seats.FirstOrDefault(seat => seat.CanTake(body)) is not { } seat || !seat.TryLock(body))
                ILog.Error($"{Name}: {body.Name} found no seat while the level closed up");
        }

        _closingUp = false;
        _count = Count;
        EmitSignal(SignalName.Changed);
    }

    // No seat with room comes before a seat that holds anything.
    private bool IsFilledInOrder()
    {
        var room = false;
        foreach (var seat in Seats)
        {
            if (room && seat.HasCards)
                return false;
            room |= seat.Holds is { } held ? seat.CanTake(held) : seat.Capacity > 0;
        }

        return true;
    }

    private ICardSpawningService? Spawning() =>
        _services != null && _services.Has<ICardSpawningService>() ? _services.Get<ICardSpawningService>() : null;

    private static Vector3 Flat(Vector3 offset) => new(offset.X, 0f, offset.Z);

    private static int? PriceOf(RigidBody3D body) => body switch
    {
        CardController card => card.ShopPrice,
        CardContainer container => container.ShopPrice,
        _ => null
    };

    private static void SetPrice(RigidBody3D body, int? price)
    {
        switch (body)
        {
            case CardController card:
                card.ShopPrice = price;
                break;
            case CardContainer container:
                container.ShopPrice = price;
                break;
        }
    }

    private static int? PriceOf(StoredItem item) => item switch
    {
        StoredCard card => card.Price,
        StoredContainer container => container.Price,
        _ => null
    };

    private static StoredItem Priced(StoredItem item, int? price) => item switch
    {
        StoredCard card => card with { Price = price },
        StoredContainer container => container with { Price = price },
        _ => item
    };
}
