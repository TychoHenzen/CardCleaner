using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The staff roles as authored in a resource file, one <see cref="StaffRoleData" /> per role.
///     <see cref="ToRoles" /> keys them by id for the staff service.
/// </summary>
[GlobalClass]
public partial class StaffCatalog : Resource
{
    /// <summary>The roles, in any order. An empty or repeated id is skipped when the catalog is read.</summary>
    [Export]
    public StaffRoleData[] Roles { get; set; } = [];

    /// <summary>
    ///     The roles keyed by id, ordinal. A role with an empty id, or an id already taken by an earlier role, is
    ///     skipped with a warning.
    /// </summary>
    public IReadOnlyDictionary<string, StaffRole> ToRoles()
    {
        var byId = new Dictionary<string, StaffRole>(StringComparer.Ordinal);
        foreach (var data in Roles)
        {
            if (data is null || string.IsNullOrWhiteSpace(data.Id))
            {
                ILog.Warning("Staff catalog has a role with no id; it is skipped.");
                continue;
            }

            if (!byId.TryAdd(data.Id, data.ToRole()))
                ILog.Warning($"Staff catalog repeats the role id '{data.Id}'; the repeat is skipped.");
        }

        return byId;
    }
}
