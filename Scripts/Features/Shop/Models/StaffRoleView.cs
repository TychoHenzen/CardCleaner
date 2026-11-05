namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One catalog role as the staff app shows it: the role itself, whether it is hired, whether its unlock is
///     reached, and the shop level that unlocks it (null when the unlock has no row).
/// </summary>
public sealed record StaffRoleView(StaffRole Role, bool Hired, bool Unlocked, int? RequiredLevel);
