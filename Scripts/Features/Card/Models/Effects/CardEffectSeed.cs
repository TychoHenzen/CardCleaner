namespace CardCleaner.Scripts.Features.Card.Models.Effects;

/// <summary>
///     The seed that places a card's scratches, from its signature alone. It is kept below <see cref="Range" />
///     because the shader feeds it to <c>sin(seed * 0.0001)</c>, which loses its precision on a large float and
///     would scatter the same scratches differently on different GPUs.
/// </summary>
public static class CardEffectSeed
{
    public const ulong Range = 10000UL;

    /// <remarks>
    ///     The signature seed is mixed before it is cut down to <see cref="Range" />: its low decimal digits alone
    ///     bunch up, because signatures in steps of 0.05 add only multiples of 50 and share 200 of the seeds.
    /// </remarks>
    public static float For(CardSignature signature)
    {
        return Mix(CardSignatureHash.Of(signature)) % Range;
    }

    // SplitMix64's finalizer: every input bit changes every output bit.
    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
