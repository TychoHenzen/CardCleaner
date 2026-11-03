using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The shop's corner HUD: the balance, the day number and the clock face. It never takes mouse input.
/// </summary>
public partial class ShopHud : Control, IDependent
{
    private const int MarginPixels = 16;

    private IShopDayService? _dayService;
    private IMoneyService? _money;
    private IServiceResolver? _services;

    public Label? MoneyLabel { get; private set; }

    public Label? DayLabel { get; private set; }

    public Label? TimeLabel { get; private set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopDayService? DayService
    {
        get => _dayService;
        set
        {
            UnsubscribeDay();
            _dayService = value;
            SubscribeDay();
            RefreshDay();
        }
    }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IMoneyService? Money
    {
        get => _money;
        set
        {
            UnsubscribeMoney();
            _money = value;
            SubscribeMoney();
            RefreshMoney();
        }
    }

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        var box = new VBoxContainer
        {
            Position = new Vector2(MarginPixels, MarginPixels),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(box);

        MoneyLabel = new Label { Name = "MoneyLabel", MouseFilter = MouseFilterEnum.Ignore };
        DayLabel = new Label { Name = "DayLabel", MouseFilter = MouseFilterEnum.Ignore };
        TimeLabel = new Label { Name = "TimeLabel", MouseFilter = MouseFilterEnum.Ignore };
        box.AddChild(MoneyLabel);
        box.AddChild(DayLabel);
        box.AddChild(TimeLabel);

        if (_services != null)
        {
            _services.Get<IShopDayService>(service => DayService ??= service);
            _services.Get<IMoneyService>(money => Money ??= money);
        }

        RefreshMoney();
        RefreshDay();
    }

    public override void _EnterTree()
    {
        UnsubscribeDay();
        SubscribeDay();
        UnsubscribeMoney();
        SubscribeMoney();
    }

    public override void _ExitTree()
    {
        UnsubscribeDay();
        UnsubscribeMoney();
    }

    private void SubscribeDay()
    {
        if (_dayService == null)
            return;
        _dayService.Opened += RefreshDay;
        _dayService.Closed += OnClosed;
        _dayService.Restored += RefreshDay;
        _dayService.TimeChanged += OnTimeChanged;
    }

    private void UnsubscribeDay()
    {
        if (_dayService == null)
            return;
        _dayService.Opened -= RefreshDay;
        _dayService.Closed -= OnClosed;
        _dayService.Restored -= RefreshDay;
        _dayService.TimeChanged -= OnTimeChanged;
    }

    private void SubscribeMoney()
    {
        if (_money != null)
            _money.BalanceChanged += OnBalanceChanged;
    }

    private void UnsubscribeMoney()
    {
        if (_money != null)
            _money.BalanceChanged -= OnBalanceChanged;
    }

    private void OnClosed(DayReport report) => RefreshDay();

    private void OnTimeChanged(int day, int minutesOfDay) => RefreshDay();

    private void OnBalanceChanged(int balance) => RefreshMoney();

    private void RefreshMoney()
    {
        if (MoneyLabel == null)
            return;
        MoneyLabel.Text = _money == null ? "Money: -" : $"Money: {_money.Balance}";
    }

    private void RefreshDay()
    {
        if (DayLabel == null || TimeLabel == null)
            return;
        if (_dayService == null)
        {
            DayLabel.Text = "Day -";
            TimeLabel.Text = "--:--";
            return;
        }

        DayLabel.Text = $"Day {_dayService.Day}";
        var minutes = _dayService.MinutesOfDay;
        TimeLabel.Text = $"{minutes / 60:D2}:{minutes % 60:D2}";
    }
}
