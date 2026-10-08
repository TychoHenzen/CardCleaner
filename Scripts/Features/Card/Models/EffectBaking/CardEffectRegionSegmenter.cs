using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Card.Models.EffectBaking;

/// <summary>
///     Splits raster art into flat colour regions by flood fill, using Axiom2d's colour and alpha thresholds. The
///     regions approximate the areas its shapes covered; they are not vector contours. Plain arrays, no Godot.
///     See <c>docs/CARD_EFFECTS_AXIOM2D.md</c> for where the port differs from Axiom2d.
/// </summary>
public static class CardEffectRegionSegmenter
{
    /// <summary>Largest colour distance (RGB in [0, 1]) between a pixel and its region's running average.</summary>
    internal const float ColorThreshold = 0.1f;

    /// <summary>Pixels with a lower alpha byte are transparent and belong to no region.</summary>
    internal const byte AlphaThreshold = 128;

    /// <summary>A region with fewer pixels than this is dropped.</summary>
    private const int MinArea = 4;

    /// <summary>The longest side of the working image, in pixels.</summary>
    public const int MaxDimension = 128;

    public static CardEffectRegionMap Segment(byte[] rgba, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgba.Length != width * height * 4)
            throw new ArgumentException("The pixel buffer must hold width x height RGBA pixels.", nameof(rgba));

        var work = ResizeToMaxDimension(rgba, width, height);
        var regions = CardEffectRegionFlood.Run(work.Rgba, work.Width, work.Height);
        var owner = new int[work.Width * work.Height];
        Array.Fill(owner, -1);
        for (var id = 0; id < regions.Count; id++)
            foreach (var pixel in regions[id].Pixels)
                owner[pixel] = id;

        // The merger absorbs every region under five pixels, so only a lone tiny region stays under MinArea;
        // Compact drops it.
        CardEffectRegionMerger.MergeSmallRegions(regions, owner, work.Width);
        return Compact(regions, owner, work.Width, work.Height);
    }

    private static WorkingImage ResizeToMaxDimension(byte[] rgba, int width, int height)
    {
        var longest = Math.Max(width, height);
        if (longest == MaxDimension) return new WorkingImage(rgba, width, height);

        var scale = MaxDimension / (float)longest;
        var dstWidth = Math.Max(1, (int)MathF.Round(width * scale));
        var dstHeight = Math.Max(1, (int)MathF.Round(height * scale));
        var resized = new byte[dstWidth * dstHeight * 4];
        for (var dy = 0; dy < dstHeight; dy++)
        for (var dx = 0; dx < dstWidth; dx++)
        {
            var sx = Math.Min(dx * width / dstWidth, width - 1);
            var sy = Math.Min(dy * height / dstHeight, height - 1);
            Array.Copy(rgba, (sy * width + sx) * 4, resized, (dy * dstWidth + dx) * 4, 4);
        }

        return new WorkingImage(resized, dstWidth, dstHeight);
    }

    /// <summary>Drops regions under <see cref="MinArea" /> and renumbers the rest 0..n-1 in id order.</summary>
    private static CardEffectRegionMap Compact(List<CardEffectRegion> regions, int[] owner, int width, int height)
    {
        var newId = new int[regions.Count];
        var kept = 0;
        for (var id = 0; id < regions.Count; id++)
            newId[id] = regions[id].Pixels.Count >= MinArea ? kept++ : -1;

        var labels = new int[owner.Length];
        for (var i = 0; i < labels.Length; i++)
            labels[i] = owner[i] < 0 ? -1 : newId[owner[i]];

        return new CardEffectRegionMap(width, height, labels, kept);
    }

    private readonly record struct WorkingImage(byte[] Rgba, int Width, int Height);
}
