namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     A baked bevel map, <see cref="Rgba" /> being four bytes per pixel, row by row. The channels are not a
///     standard normal map; the card shader decodes them as described on <see cref="CardEffectNormalBaker" />.
/// </summary>
public sealed record CardEffectNormalMap(int Width, int Height, byte[] Rgba);
