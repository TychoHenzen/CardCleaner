using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>
/// The wiring the Stock app suites share: a money service, a progression fake at shop level 2, a recording
/// ordering fake and a <see cref="StockApp" /> over a four-item catalog (cheap, dear, locked at level 4, and an
/// item with no unlock row), all registered in the test service container. The caller adds
/// <see cref="World" /> to the tree.
/// </summary>
internal sealed class StockAppFixture
{
    public const int StartingBalance = 150;
    public const int ShopLevel = 2;

    public StockAppFixture()
    {
        World = new Node();
        Money = new MoneyService { StartingBalance = StartingBalance };
        Progression = new StockProgression { Level = ShopLevel };
        Ordering = new RecordingOrdering(Money);
        App = new StockApp
        {
            Catalog = OrderTerminalFixture.MakeCatalog(("cheap", 40), ("dear", 90), ("locked", 60), ("mystery", 30))
        };

        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IMoneyService>(Money);
        TestServices.Container.RegisterSingleton<IOrderingService>(Ordering);
        TestServices.Container.RegisterSingleton<IShopProgression>(Progression);

        TestServices.Inject(App);
        World.AddChild(Money);
        World.AddChild(App);
    }

    public Node World { get; }
    public MoneyService Money { get; }
    public StockProgression Progression { get; }
    public RecordingOrdering Ordering { get; }
    public StockApp App { get; }

    /// <summary>
    /// Shop progression with fixed unlock rows: cheap and dear need level 1, locked needs level 4, and the
    /// "mystery" item has no row at all.
    /// </summary>
    internal sealed class StockProgression : IShopProgression
    {
        private readonly Dictionary<string, int> _requiredLevels = new()
        {
            ["cheap"] = 1,
            ["dear"] = 1,
            ["locked"] = 4,
        };

        public int Xp => 0;

        public int Level { get; set; }

        public float Progress => 0f;

        public bool AtMaxLevel => false;

        public event Action? ProgressChanged;

        public void AwardCheckout(int amountPaid)
        {
        }

        public bool IsUnlocked(string id) => RequiredLevel(id) is { } required && required <= Level;

        public int? RequiredLevel(string id) => _requiredLevels.TryGetValue(id, out var level) ? level : null;

        public void RaiseChanged() => ProgressChanged?.Invoke();
    }

    /// <summary>
    /// Ordering fake that records each call before it runs, so a test can see what was attempted. The first
    /// <see cref="SucceedLimit" /> calls spend the price and succeed; later calls return <see cref="FailStatus" />.
    /// </summary>
    internal sealed class RecordingOrdering : IOrderingService
    {
        private readonly IMoneyService _money;
        private int _succeeded;

        public RecordingOrdering(IMoneyService money) => _money = money;

        public List<string> Calls { get; } = [];

        public int SucceedLimit { get; set; } = int.MaxValue;

        public OrderStatus FailStatus { get; set; } = OrderStatus.DeliveryFull;

        public OrderResult Order(OrderItem item, DeliveryMarker? deliveryPoint = null)
        {
            Calls.Add(item.Id);
            if (_succeeded >= SucceedLimit)
                return new OrderResult(FailStatus);
            if (!_money.TrySpend(item.Price))
                return new OrderResult(OrderStatus.InsufficientFunds);

            _succeeded++;
            return new OrderResult(OrderStatus.Success);
        }
    }
}
