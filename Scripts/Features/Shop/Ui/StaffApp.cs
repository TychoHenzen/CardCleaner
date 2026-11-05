using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The Staff app on the backoffice PC. It lists each staff role with its hire fee, daily wage and unlock state,
///     and offers Hire, or Dismiss once the role is hired. <see cref="IStaffService" /> decides every hire and
///     dismissal; this class only shows each outcome as a message line.
/// </summary>
public partial class StaffApp : Control, IDependent
{
    private const string MessageName = "Message";

    private IServiceResolver? _services;
    private IStaffService? _staff;
    private IMoneyService? _money;
    private IShopProgression? _progression;
    private VBoxContainer _roles = null!;
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

        _services.Get<IStaffService>(staff =>
        {
            _staff = staff;
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
        if (_staff != null)
            _staff.Changed += Refresh;
        if (_money != null)
            _money.BalanceChanged += OnBalanceChanged;
        if (_progression != null)
            _progression.ProgressChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (_staff != null)
            _staff.Changed -= Refresh;
        if (_money != null)
            _money.BalanceChanged -= OnBalanceChanged;
        if (_progression != null)
            _progression.ProgressChanged -= Refresh;
    }

    private void OnBalanceChanged(int balance) => Refresh();

    // Rebuilds the role cards from the service's roles. The message line is left alone, so the outcome of a
    // hire or dismissal stays visible after the change raises its events.
    private void Refresh()
    {
        EnsureLayout();
        // Free() detaches and frees at once. Hire and Dismiss run deferred (see CardFor), so a card is never
        // freed inside its own button's Pressed signal.
        foreach (var child in _roles.GetChildren())
            child.Free();

        if (_staff == null)
            return;

        foreach (var view in _staff.Roles)
            _roles.AddChild(CardFor(view));
    }

    private PanelContainer CardFor(StaffRoleView view)
    {
        var card = new PanelContainer();
        var column = new VBoxContainer();
        card.AddChild(column);
        column.AddChild(new Label { Text = view.Role.DisplayName });
        column.AddChild(new Label { Text = $"Fee: {view.Role.HireFee}" });
        column.AddChild(new Label { Text = $"Wage: {view.Role.DailyWage} per day" });
        if (!view.Unlocked)
            column.AddChild(new Label { Text = LockText(view.RequiredLevel) });

        if (view.Hired)
        {
            var dismiss = new Button { Name = "Dismiss", Text = "Dismiss" };
            dismiss.Pressed += () => Callable.From(() => Dismiss(view)).CallDeferred();
            column.AddChild(dismiss);
        }
        else
        {
            var hire = new Button { Name = "Hire", Text = "Hire", Disabled = !view.Unlocked };
            hire.Pressed += () => Callable.From(() => Hire(view)).CallDeferred();
            column.AddChild(hire);
        }

        return card;
    }

    // The level the role needs while the shop is below it. A role with no unlock row reads as plainly locked.
    private static string LockText(int? requiredLevel) =>
        requiredLevel is int level ? $"Locked: level {level}" : "Locked";

    private void Hire(StaffRoleView view)
    {
        if (_staff == null)
        {
            _message.Text = "Staff are unavailable right now.";
            return;
        }

        _message.Text = HireMessage(_staff.Hire(view.Role.Id), view);
    }

    private void Dismiss(StaffRoleView view)
    {
        if (_staff == null)
        {
            _message.Text = "Staff are unavailable right now.";
            return;
        }

        _message.Text = DismissMessage(_staff.Dismiss(view.Role.Id), view);
    }

    private static string HireMessage(StaffResult result, StaffRoleView view)
    {
        var name = view.Role.DisplayName;
        return result switch
        {
            StaffResult.Hired => $"Hired {name}.",
            StaffResult.Locked => view.RequiredLevel is int level
                ? $"Reach shop level {level} to hire {name}."
                : $"{name} is not open to hire yet.",
            StaffResult.CannotAfford => $"Not enough money: {view.Role.HireFee} needed.",
            StaffResult.AlreadyHired => $"{name} is already hired.",
            StaffResult.UnknownRole => "That role does not exist.",
            _ => "The hire was refused.",
        };
    }

    private static string DismissMessage(StaffResult result, StaffRoleView view)
    {
        var name = view.Role.DisplayName;
        return result switch
        {
            StaffResult.Dismissed => $"Dismissed {name}.",
            StaffResult.NotHired => $"{name} is not hired.",
            StaffResult.UnknownRole => "That role does not exist.",
            _ => "The dismissal was refused.",
        };
    }

    private void EnsureLayout()
    {
        if (_layoutBuilt)
            return;
        _layoutBuilt = true;

        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(column);
        column.AddChild(new Label { Text = "Staff" });
        _roles = new VBoxContainer();
        column.AddChild(_roles);
        _message = new Label { Name = MessageName };
        column.AddChild(_message);
    }
}
