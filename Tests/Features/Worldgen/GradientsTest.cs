using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Tests.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen;

[TestSuite]
[RequireGodotRuntime]
public class GradientsTest
{
    private static readonly Vector2I MapSize = new(20, 20);
    private static readonly Vector2I[] Positions = { new(0, 0), new(5, 7), new(19, 19) };

    [TestCase]
    [TestCategory("Unit")]
    public static void FromCardsMatchesACardBasedGradientSeededTheSameWay()
    {
        var cards = MeshMapGeneratorFixture.TerrainCards();
        var a = Gradients.FromCards(cards, RandomSources.Create(4242UL));
        var b = new CardBasedGradient(cards, RandomSources.Create(4242UL));

        foreach (var position in Positions)
            AssertFloat(a.GetSignatureAt(position, MapSize).DistanceTo(b.GetSignatureAt(position, MapSize)))
                .IsEqual(0f);
    }
}
