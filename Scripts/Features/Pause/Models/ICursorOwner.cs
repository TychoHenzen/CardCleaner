namespace CardCleaner.Scripts.Features.Pause.Models;

/// <summary>
///     Owns the mouse cursor for the whole game. The cursor stays visible while any owner holds a claim.
/// </summary>
public interface ICursorOwner
{
    void Claim(object owner);

    void Release(object owner);

    bool HasClaimOtherThan(object owner);
}
