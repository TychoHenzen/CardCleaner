using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Tests.Features.Worldgen.Support;

/// <summary>
///     A <see cref="MapRequest" /> paired with the card signature that drives biome selection.
/// </summary>
public sealed record SignedMapRequest(MapRequest Request, CardSignature Signature);
