using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One staff role as authored in a resource file. <see cref="ToRole" /> converts it to the engine-free
///     <see cref="StaffRole" /> record that the roster and the cashier rules use.
/// </summary>
[GlobalClass]
public partial class StaffRoleData : Resource
{
    /// <summary>Stable identifier, unique in the staff catalog.</summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>The name shown on the staff card and the bill line.</summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The unlock id whose shop level must be reached before the role can be hired.</summary>
    [Export]
    public string UnlockId { get; set; } = string.Empty;

    /// <summary>What hiring costs, in whole money units.</summary>
    [Export]
    public int HireFee { get; set; }

    /// <summary>The wage charged each day while the role is hired.</summary>
    [Export]
    public int DailyWage { get; set; }

    /// <summary>Seconds the role takes to scan one item.</summary>
    [Export]
    public float SecondsPerScan { get; set; }

    /// <summary>Seconds the role takes to take one payment.</summary>
    [Export]
    public float SecondsPerPayment { get; set; }

    /// <summary>Seconds the role takes to take over the till before the first scan.</summary>
    [Export]
    public float TakeOverDelay { get; set; }

    public StaffRole ToRole() => new(
        Id,
        DisplayName,
        UnlockId,
        HireFee,
        DailyWage,
        SecondsPerScan,
        SecondsPerPayment,
        TakeOverDelay);
}
