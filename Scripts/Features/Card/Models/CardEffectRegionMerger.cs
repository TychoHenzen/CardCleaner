using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     Axiom2d <c>merge_small_regions</c>: merges the first region smaller than <see cref="MergeBelow" /> into its
///     nearest-colour neighbour (the nearest colour anywhere when it touches nothing), over and over. The lower id
///     survives with the area-weighted colour. A merged-away region keeps its slot, empty, so ids stay stable.
/// </summary>
internal sealed class CardEffectRegionMerger
{
    /// <summary>A region with fewer pixels than this is merged into its nearest-colour neighbour.</summary>
    private const int MergeBelow = 5;

    private readonly List<CardEffectRegion> _regions;
    private readonly int[] _owner;
    private readonly int _width;

    private CardEffectRegionMerger(List<CardEffectRegion> regions, int[] owner, int width)
    {
        _regions = regions;
        _owner = owner;
        _width = width;
    }

    /// <param name="owner">The region id of each pixel, or -1; kept in step as regions merge.</param>
    public static void MergeSmallRegions(List<CardEffectRegion> regions, int[] owner, int width)
    {
        new CardEffectRegionMerger(regions, owner, width).MergeAll();
    }

    private void MergeAll()
    {
        var live = _regions.Count;
        while (live > 1)
        {
            var small = _regions.FindIndex(region => region.Pixels.Count > 0 && region.Pixels.Count < MergeBelow);
            if (small < 0) break;

            var target = NearestTouchingRegion(small);
            if (target < 0) target = NearestRegion(small);

            Merge(Math.Min(small, target), Math.Max(small, target));
            live--;
        }
    }

    private int NearestTouchingRegion(int index)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        var source = _regions[index];
        foreach (var pixel in source.Pixels)
        foreach (var neighbour in NeighbourPixels(pixel))
        {
            var id = _owner[neighbour];
            if (id < 0 || id == index) continue;

            var distance = source.Color.DistanceSquaredTo(_regions[id].Color);
            if (distance < bestDistance || (distance == bestDistance && id < best))
            {
                bestDistance = distance;
                best = id;
            }
        }

        return best;
    }

    private int NearestRegion(int index)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var id = 0; id < _regions.Count; id++)
        {
            if (id == index || _regions[id].Pixels.Count == 0) continue;

            var distance = _regions[index].Color.DistanceSquaredTo(_regions[id].Color);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = id;
        }

        return best;
    }

    private IEnumerable<int> NeighbourPixels(int pixel)
    {
        if (pixel % _width > 0) yield return pixel - 1;
        if (pixel % _width + 1 < _width) yield return pixel + 1;
        if (pixel >= _width) yield return pixel - _width;
        if (pixel + _width < _owner.Length) yield return pixel + _width;
    }

    private void Merge(int lo, int hi)
    {
        var low = _regions[lo];
        var high = _regions[hi];
        float lowArea = low.Pixels.Count;
        float highArea = high.Pixels.Count;
        var total = lowArea + highArea;
        low.Color = new CardEffectRgb(
            (low.Color.R * lowArea + high.Color.R * highArea) / total,
            (low.Color.G * lowArea + high.Color.G * highArea) / total,
            (low.Color.B * lowArea + high.Color.B * highArea) / total);

        foreach (var pixel in high.Pixels)
            _owner[pixel] = lo;

        low.Pixels.AddRange(high.Pixels);
        high.Pixels.Clear();
    }
}
