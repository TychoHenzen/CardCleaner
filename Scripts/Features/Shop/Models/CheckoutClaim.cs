namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     Who holds the front sale of a checkout counter. One holder at a time; the player is never a holder, so a
///     held claim only ever belongs to an automated operator such as the cashier.
/// </summary>
public sealed class CheckoutClaim
{
    private object? _holder;

    /// <summary>True while some operator holds the sale.</summary>
    public bool IsHeld => _holder != null;

    /// <summary>Takes the claim. False when somebody already holds it.</summary>
    public bool TryTake(object holder)
    {
        if (_holder != null)
            return false;

        _holder = holder;
        return true;
    }

    /// <summary>Gives the claim back. A caller that is not the holder releases nothing.</summary>
    public void Release(object holder)
    {
        if (ReferenceEquals(holder, _holder))
            _holder = null;
    }

    /// <summary>Drops the claim whoever holds it, when the sale itself ends.</summary>
    public void Clear() => _holder = null;

    /// <summary>True when the claim is held and <paramref name="caller" /> is not the holder.</summary>
    public bool Excludes(object? caller) => _holder != null && !ReferenceEquals(caller, _holder);
}
