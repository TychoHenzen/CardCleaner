namespace CardCleaner.Scripts.Features.Card.Models.EffectBaking;

/// <summary>
///     Art split into colour regions: <see cref="Labels" /> holds one region id per pixel, row by row, or -1 for
///     pixels that are transparent and belong to no region. Ids run from 0 to <see cref="RegionCount" /> - 1.
/// </summary>
public sealed record CardEffectRegionMap(int Width, int Height, int[] Labels, int RegionCount);
