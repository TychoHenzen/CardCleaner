using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The hired cashier. While the shop is open and the cashier is hired, it claims the front waiting customer,
///     scans each item and takes the payment by itself, one <see cref="CashierPlan" /> action at a time. The claim
///     keeps the player out of that sale. A customer who leaves, or a shop that closes, ends the claim.
/// </summary>
public partial class CashierWorker : Node, IDependent
{
    private readonly CashierPlan _plan = new(StaffDefaults.Cashier());
    private IServiceResolver? _services;
    private IStaffService? _staff;
    private IShopDayService? _day;
    private ICheckoutCustomer? _customer;

    /// <summary>The checkout counter the cashier works.</summary>
    [Export]
    public CheckoutCounter? Counter { get; set; }

    /// <summary>The cashier's figure behind the counter. Shown only while the cashier is hired.</summary>
    [Export]
    public Node3D? Figure { get; set; }

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IStaffService>(staff =>
        {
            _staff = staff;
            staff.Changed += RefreshFigure;
            RefreshFigure();
        });
        _services.Get<IShopDayService>(day => _day = day);
    }

    public override void _ExitTree()
    {
        if (_staff != null)
            _staff.Changed -= RefreshFigure;
    }

    public override void _Process(double delta) => Step(delta);

    /// <summary>Advances the cashier by one frame. Public so tests can drive it without a frame loop.</summary>
    public void Step(double delta)
    {
        if (Counter is not { } counter)
            return;

        if (_customer != null && !KeepClaim(counter))
            EndClaim(counter);
        if (_customer == null && !TryStartClaim(counter))
            return;

        DoAction(counter, _plan.Advance((float)delta));
    }

    private bool KeepClaim(CheckoutCounter counter)
    {
        // ASSUMPTION (Low): a shop that closes mid-sale drops the claim and leaves the half-scanned sale for the
        // player to finish, because the shop's close should not take a payment on its own.
        return _day is { IsOpen: true } && counter.Session != null && _customer is { Waiting: true }
            && ReferenceEquals(counter.Front, _customer);
    }

    private bool TryStartClaim(CheckoutCounter counter)
    {
        if (_staff is null || !_staff.IsHired(StaffDefaults.CashierId) || _day is not { IsOpen: true })
            return false;
        if (counter.Front is not { Waiting: true } front || front.Basket.Items.Count < 1)
            return false;
        if (!counter.TryClaim(this))
            return false;

        _customer = front;
        _plan.Begin(front.Basket.Items.Count);
        return true;
    }

    private void DoAction(CheckoutCounter counter, CashierAction action)
    {
        switch (action)
        {
            case CashierAction.Scan:
                if (!counter.Scan(_plan.ScanIndex, this))
                {
                    ILog.Error($"Cashier could not scan item {_plan.ScanIndex}");
                    EndClaim(counter);
                }
                break;
            case CashierAction.Pay:
                TakePayment(counter);
                EndClaim(counter);
                break;
        }
    }

    /// <summary>Takes the payment once. A refused payment is logged and never retried.</summary>
    private void TakePayment(CheckoutCounter counter)
    {
        if (_customer is null || counter.Session is not { } session)
        {
            ILog.Error("Cashier had no sale to pay for");
            return;
        }

        var paid = _customer.Method == PaymentMethod.Card
            ? counter.PayCard(session.Total, this)
            : counter.PayCash(PaymentRules.ChangeParts(PaymentRules.Change(session.Tendered, session.Total)), this);
        if (!paid)
            ILog.Error("Cashier could not take the payment");
    }

    private void EndClaim(CheckoutCounter counter)
    {
        counter.ReleaseClaim(this);
        _plan.Reset();
        _customer = null;
    }

    private void RefreshFigure()
    {
        if (Figure != null)
            Figure.Visible = _staff?.IsHired(StaffDefaults.CashierId) ?? false;
    }
}
