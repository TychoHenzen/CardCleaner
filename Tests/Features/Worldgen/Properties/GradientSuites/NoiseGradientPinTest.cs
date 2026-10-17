using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties.GradientSuites;

/// <summary>
///     Pins the NoiseGradient output for fixed seeds and positions, so the noise source seam cannot change values.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NoiseGradientPinTest
{
    private static readonly int[] Seeds = [1, 42, 99999];
    private static readonly Vector2I[] Positions = [new(0, 0), new(3, 7), new(-5, 12), new(40, 40), new(-250, -17)];
    private static readonly Vector2I MapSize = new(64, 64);
    private static readonly float[] BaseValues = [0.5f, -0.5f, 0.25f, 0f, 0.75f, -0.25f, 0.1f, -0.9f];

    private static readonly float[][] Expected =
    [
        [0.5f, -0.49106324f, 0.30218613f, 0.030133339f, 0.7709201f, -0.30307424f, 0.11797306f, -0.7910882f],
        [0.45682746f, -0.49917507f, 0.1340192f, 0.12409292f, 0.8454343f, -0.2895621f, 0.0026542693f, -0.91871655f],
        [0.39836487f, -0.55333716f, 0.3686607f, -0.053625826f, 0.61984783f, -0.34833395f, 0.14995767f, -0.9652207f],
        [0.5092444f, -0.41716695f, 0.2011355f, 0.010740271f, 0.8301506f, -0.36723027f, 0.061605755f, -0.9942315f],
        [0.50743467f, -0.49586427f, 0.18257958f, -0.018243745f, 0.7041875f, -0.28004766f, 0.055170484f, -0.8723749f],
        [0.5f, -0.371838f, 0.38701713f, 0.0046088616f, 0.57850724f, -0.21690008f, 0.055559777f, -0.95413095f],
        [0.3150297f, -0.5462589f, 0.25266352f, 0.063146524f, 0.8294277f, -0.2152794f, -0.009847924f, -0.83324575f],
        [0.42909753f, -0.6084038f, 0.29349118f, -0.054540012f, 0.8512364f, -0.2800819f, 0.13794611f, -0.980862f],
        [0.51570046f, -0.546475f, 0.21297982f, -0.0520606f, 0.74973583f, -0.28815627f, 0.1376812f, -0.9640629f],
        [0.41549405f, -0.541888f, 0.31204185f, 0.04954001f, 0.65093803f, -0.18649498f, 0.017825253f, -0.8523252f],
        [0.5f, -0.56390125f, 0.27316865f, -0.012128349f, 0.77181876f, -0.13868657f, 0.12043983f, -0.9769304f],
        [0.40736938f, -0.6683308f, 0.21452194f, 0.0017705137f, 0.8262252f, -0.4341552f, 0.046764173f, -1f],
        [0.5212893f, -0.4884241f, 0.2606538f, -0.016325401f, 0.7777578f, -0.2770197f, -0.10183448f, -0.80345696f],
        [0.5335405f, -0.537429f, 0.325565f, 0.07339531f, 0.8169349f, -0.34469748f, 0.043029066f, -0.9787875f],
        [0.45129442f, -0.43497905f, 0.3207733f, -0.0022786448f, 0.8272873f, -0.20280027f, 0.17466456f, -1f],
    ];

    private static NoiseGradient Build(int seed) => new()
    {
        BaseSignature = new CardSignature(BaseValues), NoiseScale = 0.1f, NoiseStrength = 0.3f, NoiseSeed = seed
    };

    [TestCase]
    [TestCategory("Unit")]
    public void OutputIsPinnedForThreeSeedsAtFivePositions()
    {
        for (var s = 0; s < Seeds.Length; s++)
        for (var p = 0; p < Positions.Length; p++)
        {
            var signature = Build(Seeds[s]).GetSignatureAt(Positions[p], MapSize);
            for (var i = 0; i < 8; i++)
                AssertFloat(signature[i]).IsEqual(Expected[s * Positions.Length + p][i]);
        }
    }
}
