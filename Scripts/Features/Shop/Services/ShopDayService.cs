using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The shop's day. Opening moves the clock; time only runs while the shop is open. Closing settles the day's
///     bills against the balance, builds the <see cref="DayReport" /> and starts the next day. Takings are the
///     balance rises during open hours; complaints are counted for the day and reported at close.
/// </summary>
[Service(typeof(IShopDayService), typeof(IComplaintLog))]
public partial class ShopDayService : Node, IShopDayService, IComplaintLog, IDependent, ISaveable
{
    private const float DefaultOpeningMinutes = 480f;
    private const float DefaultClosingMinutes = 1080f;
    private const float DefaultDayLengthSeconds = 600f;
    private const string DayKey = "day";
    private const string DebtKey = "debt";

    private readonly ComplaintTally _tally = new();
    private IServiceResolver? _services;
    private IMoneyService? _money;
    private ShopClock? _clock;
    private int _takings;
    private int _debt;
    private int _lastBalance;
    private int _lastRaisedMinute;

    /// <summary>The clock face's opening minute.</summary>
    [Export]
    public float OpeningMinutes { get; set; } = DefaultOpeningMinutes;

    /// <summary>The clock face's closing minute.</summary>
    [Export]
    public float ClosingMinutes { get; set; } = DefaultClosingMinutes;

    /// <summary>Simulated seconds from opening to closing time.</summary>
    [Export]
    public float DayLengthSeconds { get; set; } = DefaultDayLengthSeconds;

    /// <summary>
    ///     Resolved through the injected service resolver at startup; assignable directly in tests. Changing it
    ///     moves the balance subscription to the new service.
    /// </summary>
    public IMoneyService? Money
    {
        get => _money;
        set
        {
            Unsubscribe();
            _money = value;
            Subscribe();
            _lastBalance = _money?.Balance ?? 0;
        }
    }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IDailyBillBook? Bills { get; set; }

    /// <summary>
    ///     Saves the game when the day closes. Resolved through the injected service resolver at startup; assignable
    ///     directly in tests.
    /// </summary>
    public ISaveGameCommand? SaveCommand { get; set; }

    public bool IsOpen => Clock.IsOpen;

    public int Day => Clock.Day;

    public int Debt => _debt;

    public int Takings => _takings;

    public int MinutesOfDay => (int)Clock.MinutesOfDay;

    public event Action? Opened;

    public event Action<DayReport>? Closed;

    public event Action? Restored;

    public event Action<int, int>? TimeChanged;

    // ASSUMPTION (Low): the clock is built from the exports on first use, so a unit test can set them with an
    // object initializer and no tree. The exports are read once here; changing them later has no effect.
    private ShopClock Clock => _clock ??= new ShopClock(OpeningMinutes, ClosingMinutes, DayLengthSeconds);

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            if (Money == null || Bills == null)
                ILog.Error($"{Name} was not injected");
            return;
        }

        if (Money == null)
            _services.Get<IMoneyService>(money => Money ??= money);
        if (Bills == null)
            _services.Get<IDailyBillBook>(bills => Bills ??= bills);
        if (SaveCommand == null)
            _services.Get<ISaveGameCommand>(saveCommand => SaveCommand ??= saveCommand);
    }

    public override void _EnterTree()
    {
        Unsubscribe();
        Subscribe();
    }

    public override void _ExitTree() => Unsubscribe();

    public override void _Process(double delta) => Tick(delta);

    /// <summary>Moves the clock while open. Closes the shop when closing time is reached.</summary>
    public void Tick(double delta)
    {
        if (!IsOpen)
            return;

        if (Clock.Advance((float)delta))
        {
            CloseShop();
            return;
        }

        RaiseTimeIfMinuteChanged();
    }

    public void OpenShop()
    {
        if (IsOpen)
            return;

        Clock.Open();
        _takings = 0;
        _lastBalance = Money?.Balance ?? 0;
        _lastRaisedMinute = MinutesOfDay;
        Opened?.Invoke();
        TimeChanged?.Invoke(Day, _lastRaisedMinute);
    }

    public void CloseShop()
    {
        if (!IsOpen)
            return;

        if (Money == null || Bills == null)
        {
            ILog.Error($"{Name} cannot close the day without money and bills; the shop stays open.");
            return;
        }

        // From here the shop is closed, so every balance change below is outside the day's takings.
        Clock.Close();
        var day = Clock.Day;
        var settlement = DailyBills.Settle(Bills.LinesFor(day), Money.Balance, _debt);
        if (settlement.Paid > 0)
            Money.TrySpend(settlement.Paid);

        _debt = settlement.Unpaid;
        var report = new DayReport(day, _takings, settlement, _tally.Groups());
        _tally.Clear();
        _takings = 0;
        Clock.StartNextDay();
        // The save serializes the whole tree, so it must run after TrySpend, the debt update and StartNextDay.
        if (SaveCommand?.ManualSave() != true)
            ILog.Warning("Day-close save failed or unavailable; the day is not rolled back");
        _lastRaisedMinute = MinutesOfDay;
        Closed?.Invoke(report);
        TimeChanged?.Invoke(Day, _lastRaisedMinute);
    }

    /// <summary>Records one complaint for the current day. A complaint counts whether or not the shop is open.</summary>
    public void Add(string reason) => _tally.Add(reason);

    /// <summary>Restores a saved day: the shop is closed, the debt and complaints are set back, takings are zero.</summary>
    public void Restore(int day, int debt)
    {
        Clock.Restore(day);
        _debt = Math.Max(0, debt);
        _takings = 0;
        _tally.Clear();
        _lastBalance = Money?.Balance ?? 0;
        _lastRaisedMinute = MinutesOfDay;
        Restored?.Invoke();
        TimeChanged?.Invoke(Day, _lastRaisedMinute);
    }

    public StringName UniqueID => "shop_day";

    public void Save(NodeSave save)
    {
        save.SetOrAddProperty(DayKey, Day);
        save.SetOrAddProperty(DebtKey, Debt);
    }

    public void Load(NodeSave save)
    {
        var day = ReadWhole(save, DayKey);
        var debt = ReadWhole(save, DebtKey);
        if (!DayState.TryParse(day, debt, out var state))
        {
            ILog.Error($"{Name}: saved '{DayKey}' ({Describe(day)}) or '{DebtKey}' ({Describe(debt)}) is missing or "
                + "not a whole number in range; the day is set to 1 with no debt.");
            Restore(state.Day, state.Debt);
            return;
        }

        if (state.Day != day || state.Debt != debt)
            ILog.Warning($"{Name}: saved day or debt was out of range and has been clamped to day {state.Day}, "
                + $"debt {state.Debt}.");
        // A load always lands closed at opening time, so the report is never shown again after a reload.
        Restore(state.Day, state.Debt);
    }

    // Read as object: a typed read of a corrupt value throws and would stop every later saveable's load.
    private static long? ReadWhole(NodeSave save, string key)
    {
        if (!save.TryGetProperty<object>(key, out var raw))
            return null;
        return raw switch
        {
            long whole => whole,
            int whole => whole,
            _ => (long?)null,
        };
    }

    private static string Describe(long? value) => value is { } number ? number.ToString() : "missing";

    private void RaiseTimeIfMinuteChanged()
    {
        var minute = MinutesOfDay;
        if (minute == _lastRaisedMinute)
            return;

        _lastRaisedMinute = minute;
        TimeChanged?.Invoke(Day, minute);
    }

    // BalanceChanged carries only the new balance, so a rise is takings only while open. A spend or a rise
    // while closed is never takings, but the balance is always remembered for the next comparison.
    private void OnBalanceChanged(int balance)
    {
        if (IsOpen && balance > _lastBalance)
            _takings += balance - _lastBalance;
        _lastBalance = balance;
    }

    private void Subscribe()
    {
        if (_money != null)
            _money.BalanceChanged += OnBalanceChanged;
    }

    private void Unsubscribe()
    {
        if (_money != null)
            _money.BalanceChanged -= OnBalanceChanged;
    }
}
