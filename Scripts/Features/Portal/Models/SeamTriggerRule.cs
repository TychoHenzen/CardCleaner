using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Portal.Models;

/// <summary>
///     Decides when the backoffice wall seam shows: only while the player stands in the backoffice and
///     carries at least one special (magical) card. Common cards have an all-zero signature and never
///     trigger it. A card without a signature counts as common.
/// </summary>
public static class SeamTriggerRule
{
    public static bool ShouldShow(bool isInBackoffice, IEnumerable<CardSignature?> carriedCards)
    {
        return isInBackoffice && carriedCards.Any(signature => signature is { } s && s.HasMagicalPotential());
    }
}
