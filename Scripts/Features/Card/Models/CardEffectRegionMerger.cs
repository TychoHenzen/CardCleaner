using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     Axiom2d <c>merge_small_regions</c>: merges the first region smaller than the limit into its nearest-colour
///     neighbour (the nearest colour anywhere when it touches nothing), over and over. The lower id survives with
///     the area-weighted colour. A merged-away region keeps its slot, empty, so ids stay stable.
/// </summary>
internal static class CardEffectRegionMerger
{
    public static void MergeSmallRegions(List<CardEffectRegion> regions, int[] owner, int width, int mergeBelow)
    {
        var live = regions.Count;
        while (live > 1)
        {
            var small = regions.FindIndex(region => region.Pixels.Count > 0 && region.Pixels.Count < mergeBelow);
            if (small < 0) break;

            var target = NearestTouchingRegion(regions, owner, width, small);
            if (target < 0) target = NearestRegion(regions, small);

            Merge(regions, owner, Math.Min(small, target), Math.Max(small, target));
            live--;
        }
    }

    private static int NearestTouchingRegion(List<CardEffectRegion> regions, int[] owner, int width, int index)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        var source = regions[index];
        foreach (var pixel in source.Pixels)
        foreach (var neighbour in NeighbourPixels(pixel, width, owner.Length))
        {
            var id = owner[neighbour];
            if (id < 0 || id == index) continue;

            var distance = source.Color.DistanceSquaredTo(regions[id].Color);
            if (distance < bestDistance || (distance == bestDistance && id < best))
            {
                bestDistance = distance;
                best = id;
            }
        }

        return best;
    }

    private static int NearestRegion(List<CardEffectRegion> regions, int index)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var id = 0; id < regions.Count; id++)
        {
            if (id == index || regions[id].Pixels.Count == 0) continue;

            var distance = regions[index].Color.DistanceSquaredTo(regions[id].Color);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = id;
        }

        return best;
    }

    private static IEnumerable<int> NeighbourPixels(int pixel, int width, int pixelCount)
    {
        if (pixel % width > 0) yield return pixel - 1;
        if (pixel % width + 1 < width) yield return pixel + 1;
        if (pixel >= width) yield return pixel - width;
        if (pixel + width < pixelCount) yield return pixel + width;
    }

    private static void Merge(List<CardEffectRegion> regions, int[] owner, int lo, int hi)
    {
        var low = regions[lo];
        var high = regions[hi];
        float lowArea = low.Pixels.Count;
        float highArea = high.Pixels.Count;
        var total = lowArea + highArea;
        low.Color = new CardEffectRgb(
            (low.Color.R * lowArea + high.Color.R * highArea) / total,
            (low.Color.G * lowArea + high.Color.G * highArea) / total,
            (low.Color.B * lowArea + high.Color.B * highArea) / total);

        foreach (var pixel in high.Pixels)
            owner[pixel] = lo;

        low.Pixels.AddRange(high.Pixels);
        high.Pixels.Clear();
    }
}
