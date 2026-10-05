namespace CardCleaner.Scripts.Features.Portal.Models;

/// <summary>
///     The seam's state machine. Without a trigger the seam is always hidden, so dropping the card or
///     leaving the backoffice closes it at once, whatever phase it was in. With a trigger it glows, and it
///     opens when the player comes within <paramref name="openRange" />. Once open it only closes again
///     past <paramref name="closeRange" />, so standing at the edge of the range does not flicker it.
/// </summary>
public static class SeamPhaseRule
{
    public static SeamPhase Next(SeamPhase current, bool triggered, float distance, float openRange, float closeRange)
    {
        if (!triggered)
            return SeamPhase.Hidden;

        var openLimit = current == SeamPhase.Open ? closeRange : openRange;
        return distance <= openLimit ? SeamPhase.Open : SeamPhase.Glowing;
    }
}
