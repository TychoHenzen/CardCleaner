namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The outcome of a hire or dismiss request.</summary>
public enum StaffResult
{
    Hired,
    Dismissed,
    Locked,
    CannotAfford,
    AlreadyHired,
    NotHired,
    UnknownRole,
}
