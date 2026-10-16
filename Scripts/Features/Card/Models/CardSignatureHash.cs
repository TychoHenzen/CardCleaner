using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Numerics;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     The hash of a signature's element values, from which card seeds are derived. It is computed from the values
///     alone, with no runtime-randomised hashing, so it is the same in every run; the pinned seed tests depend on that.
///     It also draws random signatures from an injected random source.
/// </summary>
public static class CardSignatureHash
{
    public static ulong Of(CardSignature signature)
    {
        var seed = 17UL;
        foreach (var v in signature.Elements)
            seed = seed * 23UL + (ulong)CoreMath.RoundToInt(v * 1000);
        return seed;
    }

    public static CardSignature Create(IRandom random)
    {
        var elements = new float[8];
        for (var index = 0; index < elements.Length; index++)
            elements[index] = random.RandfRange(-1f, 1f);
        return new CardSignature(elements);
    }

    public static CardSignature CreateSpecial(IRandom random)
    {
        var signature = Create(random);
        while (!signature.HasMagicalPotential())
            signature = Create(random);
        return signature;
    }
}
