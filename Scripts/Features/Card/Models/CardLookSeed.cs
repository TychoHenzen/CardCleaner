using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Card.Models;

public static class CardLookSeed
{
    /// <summary>
    ///     A mundane card has no signature of its own to look at, so its look comes from this seed. A card with
    ///     magical potential has none: its look comes from its signature.
    /// </summary>
    public static int? Resolve(CardSignature signature, int? saved, IRandom random)
    {
        if (signature.HasMagicalPotential())
            return null;

        return saved ?? unchecked((int)random.Randi());
    }
}
