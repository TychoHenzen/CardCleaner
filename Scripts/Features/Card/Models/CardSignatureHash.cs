using Godot;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     The hash of a signature's element values, from which card seeds are derived. It is computed from the values
///     alone, with no runtime-randomised hashing, so it is the same in every run; the pinned seed tests depend on that.
/// </summary>
public static class CardSignatureHash
{
    public static ulong Of(CardSignature signature)
    {
        var seed = 17UL;
        foreach (var v in signature.Elements)
            seed = seed * 23UL + (ulong)Mathf.RoundToInt(v * 1000);
        return seed;
    }
}
