using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Tests.Features.Worldgen.Support;
using FsCheck;

namespace CardCleaner.Tests.Features.Worldgen.Properties.MapGenerationSuites;

/// <summary>
///     Property-based integration tests: maps stay valid for extreme and random card signatures (ST009).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class MapSignatureProperties : MapGenerationPropertyBase
{
    [TestCase]
    public void Map_WithExtremeNegativeSignature_RemainsValid()
    {
        // All -1 signature (extreme cold, dark, chaotic, etc.)
        MapInvariantsHoldFor(new CardSignature(new[] { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f }), 30);
    }

    [TestCase]
    public void Map_WithExtremePositiveSignature_RemainsValid()
    {
        // All +1 signature (extreme hot, bright, orderly, etc.)
        MapInvariantsHoldFor(new CardSignature(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }), 30);
    }

    [TestCase]
    public void Map_WithNeutralSignature_RemainsValid()
    {
        // All 0 signature (neutral/balanced)
        MapInvariantsHoldFor(new CardSignature(), 30);
    }

    [TestCase]
    public void Map_WithRandomSignature_RemainsValid()
    {
        Property(p => p
            .ForAll(
                Arb.From(SignedMapRequests(8, 15)),
                args => MapInvariants.Validate(
                    GenerateMap(args.Request, args.Signature),
                    args.Request.Width,
                    args.Request.Height))
            .Iterations(50));
    }

    [TestCase]
    public void Map_WithMixedExtremeSignature_RemainsValid()
    {
        // Alternating extremes: -1, +1, -1, +1, etc.
        MapInvariantsHoldFor(new CardSignature(new[] { -1f, 1f, -1f, 1f, -1f, 1f, -1f, 1f }), 30);
    }
}
