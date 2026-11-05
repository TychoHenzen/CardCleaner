using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
/// The hired staff, kept in hire order. Holds ids only; the role records come from a catalog and the money
/// from <c>IMoneyService</c>, so this class never touches money.
/// </summary>
public sealed class StaffRoster
{
    private readonly List<string> _hired = new();

    /// <summary>The hired role ids, in the order they were hired.</summary>
    public IReadOnlyCollection<string> HiredRoleIds => _hired;

    public bool IsHired(string id) => _hired.Contains(id);

    /// <summary>
    /// Says whether a hire would succeed. Checks, in order: a missing role, an already hired role, a role
    /// whose unlock is not reached, then a balance below the hire fee. Never changes the roster or the money.
    /// </summary>
    public StaffResult Decide(StaffRole? role, bool unlocked, int balance)
    {
        if (role is null)
        {
            return StaffResult.UnknownRole;
        }

        if (IsHired(role.Id))
        {
            return StaffResult.AlreadyHired;
        }

        if (!unlocked)
        {
            return StaffResult.Locked;
        }

        if (balance < role.HireFee)
        {
            return StaffResult.CannotAfford;
        }

        return StaffResult.Hired;
    }

    /// <summary>Adds a hired role id. An id already present is ignored.</summary>
    public void Add(string roleId)
    {
        if (!IsHired(roleId))
        {
            _hired.Add(roleId);
        }
    }

    /// <summary>Removes a hired role id. Returns <see cref="StaffResult.NotHired"/> when it is absent.</summary>
    public StaffResult Dismiss(string roleId)
    {
        if (!_hired.Remove(roleId))
        {
            return StaffResult.NotHired;
        }

        return StaffResult.Dismissed;
    }

    /// <summary>The hired roles in hire order. Ids the catalog does not hold are skipped.</summary>
    public IReadOnlyList<StaffRole> HiredRoles(IReadOnlyDictionary<string, StaffRole> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var roles = new List<StaffRole>();
        foreach (var id in _hired)
        {
            if (catalog.TryGetValue(id, out var role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    /// <summary>
    /// Replaces the roster with the known, distinct ids, keeping the first occurrence and the given order.
    /// Returns how many ids were dropped, whether unknown or repeated.
    /// </summary>
    public int Restore(IEnumerable<string> ids, Func<string, bool> isKnown)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(isKnown);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<string>();
        var dropped = 0;
        foreach (var id in ids)
        {
            if (!isKnown(id) || !seen.Add(id))
            {
                dropped++;
                continue;
            }

            kept.Add(id);
        }

        _hired.Clear();
        _hired.AddRange(kept);
        return dropped;
    }
}
