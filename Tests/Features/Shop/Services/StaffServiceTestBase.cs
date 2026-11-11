using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Services;

/// <summary>
///     The shared world, money, staff service and catalogs of the staff service suites: a staff service with the
///     cashier role, a thousand coins and a progression that unlocks everything.
/// </summary>
[RequireGodotRuntime]
public abstract class StaffServiceTestBase
{
    private protected const int StartingBalance = 1000;
    private protected const string CashierId = "cashier";
    private protected const string CashierUnlock = "staff_cashier";
    private protected const int CashierFee = 200;

    private protected Node3D _world = null!;
    private protected MoneyService _money = null!;
    private protected StaffService _staff = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        AddNode(_world);

        _money = new MoneyService { StartingBalance = StartingBalance };
        _world.AddChild(_money);

        _staff = NewStaffService();
    }

    private protected StaffService NewStaffService() => AutoFree(new StaffService
    {
        Catalog = CashierCatalog(),
        Money = _money,
        Progression = new OpenProgression(),
    });

    private protected static StaffCatalog CashierCatalog() => new()
    {
        Roles =
        [
            new StaffRoleData
            {
                Id = CashierId, DisplayName = "Cashier", UnlockId = CashierUnlock, HireFee = CashierFee,
                DailyWage = 40, SecondsPerScan = 1.5f, SecondsPerPayment = 2f, TakeOverDelay = 3f,
            },
        ],
    };

    private protected static StaffCatalog CashierAndRestockerCatalog() => new()
    {
        Roles =
        [
            CashierCatalog().Roles[0],
            new StaffRoleData
            {
                Id = StaffDefaults.RestockerId, DisplayName = StaffDefaults.RestockerName,
                UnlockId = StaffDefaults.RestockerUnlockId, HireFee = StaffDefaults.RestockerHireFee,
                DailyWage = StaffDefaults.RestockerDailyWage,
            },
        ],
    };
}
