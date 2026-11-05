using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;
using Saveable;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Hires and dismisses the shop's staff. The roles come from the exported <see cref="Catalog" />, the hired ids
///     live in a <see cref="StaffRoster" />, and the fee is charged through <see cref="IMoneyService" /> only after the
///     roster accepts the hire. A role whose unlock is not reached, or with no progression service present, is refused
///     as Locked (fails closed).
/// </summary>
[Service(typeof(IStaffService))]
public partial class StaffService : Node, IStaffService, IDependent, ISaveable
{
    private const string HiredKey = "hired";
    private static readonly IReadOnlyDictionary<string, StaffRole> NoRoles = new Dictionary<string, StaffRole>();

    private readonly StaffRoster _roster = new();
    private IServiceResolver? _services;

    /// <summary>The roles the shop sells, in any order. Assign it before the first hire.</summary>
    [Export]
    public StaffCatalog? Catalog { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IMoneyService? Money { get; set; }

    /// <summary>Resolved through the injected service resolver at startup; assignable directly in tests.</summary>
    public IShopProgression? Progression { get; set; }

    /// <summary>Raised after a hire, a dismissal or a load changes the roster.</summary>
    public event Action? Changed;

    public StringName UniqueID => "staff";

    public IReadOnlyList<StaffRole> HiredRoles => _roster.HiredRoles(Catalogue());

    public IReadOnlyList<StaffRoleView> Roles =>
        Catalogue()
            .Values
            .Select(role => new StaffRoleView(
                role,
                _roster.IsHired(role.Id),
                IsUnlocked(role),
                Progression?.RequiredLevel(role.UnlockId)))
            .ToList();

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IMoneyService>(money => Money ??= money);
        _services.Get<IShopProgression>(progression => Progression ??= progression);
    }

    public bool IsHired(string roleId) => _roster.IsHired(roleId);

    /// <summary>
    ///     Hires a role when the roster accepts it: the role is in the catalog, its unlock is reached and the balance
    ///     covers the fee. The fee is charged once and the role is added; a refused hire changes neither.
    /// </summary>
    public StaffResult Hire(string roleId)
    {
        var role = FindRole(roleId);
        var result = _roster.Decide(role, IsUnlocked(role), Money?.Balance ?? 0);
        if (role is null || result != StaffResult.Hired)
            return result;
        if (Money is null || !Money.TrySpend(role.HireFee))
            return StaffResult.CannotAfford;

        _roster.Add(roleId);
        Changed?.Invoke();
        return StaffResult.Hired;
    }

    /// <summary>
    ///     Dismisses a hired role. Nothing is refunded, and <see cref="Changed" /> is raised only on success.
    /// </summary>
    public StaffResult Dismiss(string roleId)
    {
        var result = _roster.Dismiss(roleId);
        if (result == StaffResult.Dismissed)
            Changed?.Invoke();
        return result;
    }

    /// <summary>Saves the hired role ids, in hire order, as one JSON list.</summary>
    public void Save(NodeSave save) =>
        save.SetOrAddProperty(HiredKey, JsonSerializer.Serialize(_roster.HiredRoleIds.ToList()));

    /// <summary>
    ///     Replaces the hired roles with the saved ids that the catalog still holds. An id that is unknown or repeated
    ///     is logged and dropped, and a list that cannot be read leaves the roster as it is.
    /// </summary>
    public void Load(NodeSave save)
    {
        if (!save.TryGetProperty<string>(HiredKey, out var json) || json is null)
            return;

        List<string>? saved;
        try
        {
            saved = JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            saved = null;
        }

        if (saved is null)
        {
            ILog.Warning("Staff save is unreadable; the hired roles are unchanged");
            return;
        }

        var catalogue = Catalogue();
        var dropped = _roster.Restore(saved, id => id is not null && catalogue.ContainsKey(id));
        if (dropped > 0)
            ILog.Warning($"Staff save names {dropped} unknown or repeated role id(s); they are ignored");

        Changed?.Invoke();
    }

    private IReadOnlyDictionary<string, StaffRole> Catalogue() => Catalog?.ToRoles() ?? NoRoles;

    private StaffRole? FindRole(string roleId)
    {
        var roles = Catalogue();
        return roles.TryGetValue(roleId, out var role) ? role : null;
    }

    private bool IsUnlocked(StaffRole? role) =>
        role is not null && Progression is { } progression && progression.IsUnlocked(role.UnlockId);
}
