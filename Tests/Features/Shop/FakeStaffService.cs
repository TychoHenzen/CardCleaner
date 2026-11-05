using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>
///     A staff roster with no money and no unlocks: the roles it knows are fixed at construction, and a test sets the
///     hired ids directly. Hire and Dismiss change the ids and raise <see cref="Changed" />, but charge nothing.
/// </summary>
public sealed class FakeStaffService : IStaffService
{
    private readonly List<StaffRole> _roles;

    public FakeStaffService(params StaffRole[] roles)
    {
        _roles = new List<StaffRole>(roles);
    }

    /// <summary>The hired role ids. A test adds to it to hire a role without going through Hire.</summary>
    public List<string> HiredIds { get; } = new();

    public event Action? Changed;

    /// <summary>Swaps the role with the same id, as an edited catalog would. Raises nothing.</summary>
    public void Replace(StaffRole role)
    {
        _roles.RemoveAll(existing => existing.Id == role.Id);
        _roles.Add(role);
    }

    public bool IsHired(string roleId) => HiredIds.Contains(roleId);

    public IReadOnlyList<StaffRole> HiredRoles => _roles.FindAll(role => HiredIds.Contains(role.Id));

    public IReadOnlyList<StaffRoleView> Roles =>
        _roles.ConvertAll(role => new StaffRoleView(role, HiredIds.Contains(role.Id), true, 1));

    public StaffResult Hire(string roleId)
    {
        var role = _roles.Find(candidate => candidate.Id == roleId);
        if (role is null)
            return StaffResult.UnknownRole;
        if (HiredIds.Contains(roleId))
            return StaffResult.AlreadyHired;

        HiredIds.Add(roleId);
        Changed?.Invoke();
        return StaffResult.Hired;
    }

    public StaffResult Dismiss(string roleId)
    {
        if (!HiredIds.Remove(roleId))
            return StaffResult.NotHired;

        Changed?.Invoke();
        return StaffResult.Dismissed;
    }
}
