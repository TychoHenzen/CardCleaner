using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>The shop's hired staff, and the hire and dismiss rules over them.</summary>
public interface IStaffService
{
    /// <summary>Raised after a hire, a dismissal or a load changes the roster.</summary>
    event Action? Changed;

    bool IsHired(string roleId);

    /// <summary>The hired roles, in hire order.</summary>
    IReadOnlyList<StaffRole> HiredRoles { get; }

    /// <summary>Every catalog role, with whether it is hired and whether its unlock is reached.</summary>
    IReadOnlyList<StaffRoleView> Roles { get; }

    /// <summary>Hires the role when the roster accepts it, charging its fee once.</summary>
    StaffResult Hire(string roleId);

    /// <summary>Dismisses the role. Nothing is refunded.</summary>
    StaffResult Dismiss(string roleId);
}
