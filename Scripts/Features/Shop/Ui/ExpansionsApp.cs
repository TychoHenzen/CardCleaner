using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The Expansions app on the backoffice PC. It lists the storefront lots in purchase order with their price, rent,
///     extra customers and state, and offers Buy on the next lot. <see cref="IShopExpansion.TryBuy" /> decides every
///     purchase; this class only shows its outcome as a message line.
/// </summary>
public partial class ExpansionsApp : Control, IDependent
{
    private const string MessageName = "Message";

    private IServiceResolver? _services;
    private IShopExpansion? _expansion;
    private IMoneyService? _money;
    private IShopProgression? _progression;
    private VBoxContainer _lots = null!;
    private Label _message = null!;
    private bool _layoutBuilt;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        Visible = false;
        EnsureLayout();
        VisibilityChanged += Refresh;

        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IShopExpansion>(expansion =>
        {
            _expansion = expansion;
            Subscribe();
            Refresh();
        });
        _services.Get<IMoneyService>(money =>
        {
            _money = money;
            Subscribe();
            Refresh();
        });
        _services.Get<IShopProgression>(progression =>
        {
            _progression = progression;
            Subscribe();
            Refresh();
        });
    }

    // _Ready runs once, but the app can leave and re-enter the tree, so its subscriptions are restored
    // on every entry and dropped on every exit.
    public override void _EnterTree() => Subscribe();

    public override void _ExitTree() => Unsubscribe();

    private void Subscribe()
    {
        if (!IsInsideTree())
            return;

        Unsubscribe();
        if (_expansion != null)
            _expansion.Opened += OnLotOpened;
        if (_money != null)
            _money.BalanceChanged += OnBalanceChanged;
        if (_progression != null)
            _progression.ProgressChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (_expansion != null)
            _expansion.Opened -= OnLotOpened;
        if (_money != null)
            _money.BalanceChanged -= OnBalanceChanged;
        if (_progression != null)
            _progression.ProgressChanged -= Refresh;
    }

    private void OnLotOpened(string lotId) => Refresh();

    private void OnBalanceChanged(int balance) => Refresh();

    // Rebuilds the lot cards from the service's plan. The message line is left alone, so the outcome of a
    // purchase stays visible after the purchase raises its events.
    private void Refresh()
    {
        EnsureLayout();
        // Free() detaches and frees at once, so no detached card waits for a frame as an orphan. Buy runs deferred
        // (see CardFor), so a card is never freed inside its own button's Pressed signal.
        foreach (var child in _lots.GetChildren())
            child.Free();

        var plan = _expansion?.Plan;
        if (plan == null)
            return;

        ExpansionLot? previous = null;
        foreach (var lot in plan.Lots)
        {
            _lots.AddChild(CardFor(lot, plan, previous));
            previous = lot;
        }
    }

    private PanelContainer CardFor(ExpansionLot lot, ExpansionPlan plan, ExpansionLot? previous)
    {
        var card = new PanelContainer();
        var column = new VBoxContainer();
        card.AddChild(column);
        column.AddChild(new Label { Text = lot.DisplayName });
        column.AddChild(new Label { Text = $"Price {lot.Price}" });
        column.AddChild(new Label { Text = $"Rent {lot.DailyRent}" });
        column.AddChild(new Label { Text = $"+{lot.ExtraCustomers} customers" });
        column.AddChild(new Label { Text = StateOf(lot, plan, previous) });
        var locked = LockText(lot, plan);
        if (locked != null)
            column.AddChild(new Label { Text = locked });

        if (lot.Id == plan.Next?.Id)
        {
            var buy = new Button { Text = "Buy" };
            buy.Pressed += () => Callable.From(() => Buy(lot)).CallDeferred();
            column.AddChild(buy);
        }

        return card;
    }

    private static string StateOf(ExpansionLot lot, ExpansionPlan plan, ExpansionLot? previous)
    {
        if (plan.Owned.Contains(lot.Id))
            return "Owned";
        if (lot.Id == plan.Next?.Id)
            return "Next";
        // Neither owned nor next, so the next lot comes earlier in the order and this lot has a predecessor.
        return $"After {previous!.DisplayName}";
    }

    // The level the lot needs while the shop is below it. A lot with no unlock row reads as plainly locked.
    private string? LockText(ExpansionLot lot, ExpansionPlan plan)
    {
        if (plan.Owned.Contains(lot.Id) || (_progression?.IsUnlocked(lot.UnlockId) ?? false))
            return null;
        var level = _progression?.RequiredLevel(lot.UnlockId);
        return level is int required ? $"Locked level {required}" : "Locked";
    }

    private void Buy(ExpansionLot lot)
    {
        if (_expansion == null)
        {
            _message.Text = "Expansions are unavailable right now.";
            return;
        }

        _message.Text = MessageFor(_expansion.TryBuy(lot.Id), lot);
    }

    private string MessageFor(PurchaseOutcome outcome, ExpansionLot lot) => outcome switch
    {
        PurchaseOutcome.Bought => $"Bought {lot.DisplayName}. Its wall is open.",
        PurchaseOutcome.OutOfOrder => "Buy the earlier lots first.",
        PurchaseOutcome.Locked => _progression?.RequiredLevel(lot.UnlockId) is int level
            ? $"Reach shop level {level} to buy {lot.DisplayName}."
            : $"{lot.DisplayName} is not open to buy yet.",
        PurchaseOutcome.Unaffordable => $"Not enough money: {lot.Price} needed.",
        PurchaseOutcome.AlreadyOwned => $"{lot.DisplayName} is already owned.",
        PurchaseOutcome.Unknown => "That lot does not exist.",
        _ => "The purchase was refused.",
    };

    private void EnsureLayout()
    {
        if (_layoutBuilt)
            return;
        _layoutBuilt = true;

        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(column);
        column.AddChild(new Label { Text = "Expansions" });
        _lots = new VBoxContainer();
        column.AddChild(_lots);
        _message = new Label { Name = MessageName };
        column.AddChild(_message);
    }
}
