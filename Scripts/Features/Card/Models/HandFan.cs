using System;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     How the cards in the player's hand fan out. Neighbouring cards are turned apart by the same angle until
///     <c>fillCount</c> cards fill the whole spread; from then on the fan keeps its full spread and every extra card
///     closes the gaps. Angles are in degrees, centred on the middle of the hand: negative to the left, positive to
///     the right.
/// </summary>
public static class HandFan
{
    /// <summary>
    ///     The angle between two neighbouring cards when <paramref name="count" /> cards are held: the spread over
    ///     <paramref name="fillCount" /> - 1 gaps while the hand holds no more than <paramref name="fillCount" />, else
    ///     the spread over <paramref name="count" /> - 1 gaps. 0 for fewer than two cards.
    /// </summary>
    public static float StepDegrees(int count, float spreadDegrees, int fillCount)
    {
        if (count < 2 || spreadDegrees <= 0f)
            return 0f;

        var gaps = Math.Max(count, Math.Max(fillCount, 2)) - 1;
        return spreadDegrees / gaps;
    }

    /// <summary>The angle of the card at <paramref name="index" /> (0 is the leftmost) of <paramref name="count" />.</summary>
    public static float AngleDegrees(int index, int count, float spreadDegrees, int fillCount) =>
        (index - (count - 1) / 2f) * StepDegrees(count, spreadDegrees, fillCount);
}
