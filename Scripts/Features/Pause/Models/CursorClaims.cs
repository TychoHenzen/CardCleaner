using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Pause.Models;

/// <summary>
///     The set of owners currently holding the cursor visible. Owners are compared by reference.
/// </summary>
public sealed class CursorClaims
{
    private readonly HashSet<object> _owners = new(ReferenceEqualityComparer.Instance);

    public bool HasClaims => _owners.Count > 0;

    public void Claim(object owner) => _owners.Add(owner);

    public void Release(object owner) => _owners.Remove(owner);

    public bool HasClaimOtherThan(object owner) => _owners.Any(o => !ReferenceEquals(o, owner));
}
