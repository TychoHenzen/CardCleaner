using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Where a cashier is in serving one customer.</summary>
public enum CashierStep
{
    Idle,
    Claiming,
    Scanning,
    Paying,
    Done,
}

/// <summary>What the cashier does on an <see cref="CashierPlan.Advance"/> call.</summary>
public enum CashierAction
{
    None,
    Scan,
    Pay,
}

/// <summary>
/// The timing of one cashier serving one customer: take over the till, scan each item, then take payment.
/// Pure time-driven rules with no Godot types. Each <see cref="Advance"/> call makes at most one transition,
/// so the caller loops to drain a large time step.
/// </summary>
public sealed class CashierPlan
{
    private float _elapsed;
    private int _itemCount;

    public CashierPlan(StaffRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        Role = role;
    }

    public StaffRole Role { get; }

    public CashierStep Step { get; private set; } = CashierStep.Idle;

    /// <summary>
    /// The 0-based index of the item the last <see cref="Advance"/> scanned, or -1 before the first scan.
    /// </summary>
    public int ScanIndex { get; private set; } = -1;

    /// <summary>Starts serving a customer with the given number of items. Needs at least one item.</summary>
    public void Begin(int itemCount)
    {
        if (itemCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount), itemCount, "A customer needs at least one item.");
        }

        _itemCount = itemCount;
        Step = CashierStep.Claiming;
        ScanIndex = -1;
        _elapsed = 0;
    }

    /// <summary>
    /// Adds the elapsed seconds and makes at most one transition. Returns <see cref="CashierAction.None"/> and
    /// changes nothing when the seconds are not positive or the plan is idle or done.
    /// </summary>
    public CashierAction Advance(float seconds)
    {
        if (seconds <= 0 || Step is CashierStep.Idle or CashierStep.Done)
        {
            return CashierAction.None;
        }

        _elapsed += seconds;
        var threshold = Threshold();
        if (_elapsed < threshold)
        {
            return CashierAction.None;
        }

        _elapsed -= threshold;
        switch (Step)
        {
            case CashierStep.Claiming:
                Step = CashierStep.Scanning;
                return CashierAction.None;
            case CashierStep.Scanning:
                ScanIndex++;
                if (ScanIndex + 1 == _itemCount)
                {
                    Step = CashierStep.Paying;
                }

                return CashierAction.Scan;
            case CashierStep.Paying:
                Step = CashierStep.Done;
                return CashierAction.Pay;
            default:
                return CashierAction.None;
        }
    }

    /// <summary>Returns the plan to idle. Used when the cashier leaves the till.</summary>
    public void Reset()
    {
        Step = CashierStep.Idle;
    }

    private float Threshold() => Step switch
    {
        CashierStep.Claiming => Role.TakeOverDelay,
        CashierStep.Scanning => Role.SecondsPerScan,
        _ => Role.SecondsPerPayment,
    };
}
