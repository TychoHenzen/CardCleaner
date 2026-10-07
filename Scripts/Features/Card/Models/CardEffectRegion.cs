using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>One colour region while the segmenter grows and merges it.</summary>
internal sealed class CardEffectRegion
{
    private float _sumR;
    private float _sumG;
    private float _sumB;

    public List<int> Pixels { get; } = new();

    /// <summary>The average colour of the region's pixels; kept up to date by the segmenter, not by <see cref="Add" />.</summary>
    public CardEffectRgb Color { get; set; }

    /// <summary>The mean of the pixels added so far; only meaningful once there is one.</summary>
    public CardEffectRgb RunningAverage
    {
        get
        {
            var inverse = 1f / Pixels.Count;
            return new CardEffectRgb(_sumR * inverse, _sumG * inverse, _sumB * inverse);
        }
    }

    public void Add(int pixel, CardEffectRgb color)
    {
        Pixels.Add(pixel);
        _sumR += color.R;
        _sumG += color.G;
        _sumB += color.B;
    }
}
