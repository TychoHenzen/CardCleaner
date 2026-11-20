using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>Draws random card signatures from an injected random source.</summary>
public static class SignatureRandom
{
    public static CardSignature Create(IRandom random)
    {
        var elements = new float[8];
        for (var index = 0; index < elements.Length; index++)
            elements[index] = random.RandfRange(-1f, 1f);
        return new CardSignature(elements);
    }

    /// <summary>Every element of a faint signature lies within this distance of zero.</summary>
    public const float FaintCap = 0.05f;

    public static CardSignature CreateSpecial(IRandom random)
    {
        var signature = Create(random);
        while (!signature.HasMagicalPotential())
            signature = Create(random);
        return signature;
    }

    /// <summary>
    ///     A signature only just above mundane: every element within <see cref="FaintCap" />, one not zero.
    /// </summary>
    public static CardSignature CreateFaint(IRandom random)
    {
        var signature = CreateCapped(random);
        while (!signature.HasMagicalPotential())
            signature = CreateCapped(random);
        return signature;
    }

    private static CardSignature CreateCapped(IRandom random)
    {
        var elements = new float[8];
        for (var index = 0; index < elements.Length; index++)
            elements[index] = random.RandfRange(-FaintCap, FaintCap);
        return new CardSignature(elements);
    }
}
