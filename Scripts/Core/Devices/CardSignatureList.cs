using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Core.Devices;

/// <summary>
/// Collection of card signatures, typically from chained card slots.
/// </summary>
public class CardSignatureList
{
    public IReadOnlyList<CardSignature> Signatures { get; }

    public CardSignatureList(IEnumerable<CardSignature> signatures)
    {
        Signatures = signatures.ToList();
    }

    public CardSignatureList(CardSignature single)
    {
        Signatures = [single];
    }

    public static CardSignatureList Empty => new([]);
}
