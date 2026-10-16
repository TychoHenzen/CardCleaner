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

    public static CardSignature CreateSpecial(IRandom random)
    {
        var signature = Create(random);
        while (!signature.HasMagicalPotential())
            signature = Create(random);
        return signature;
    }
}
