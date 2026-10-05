using System;
using CardCleaner.Scripts.Features.Portal.Models;

namespace CardCleaner.Tests.Features.Portal.Models;

[TestSuite]
[RequireGodotRuntime]
public class SeamToneTest
{
    private static short SampleAt(byte[] pcm, int index)
    {
        return (short)(pcm[index * 2] | (pcm[index * 2 + 1] << 8));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ToneIsWholePeriodsOfSixteenBitMonoSamples()
    {
        var pcm = SeamTone.Pcm16();

        AssertThat(pcm.Length % 2).IsEqual(0);
        AssertThat(pcm.Length / 2).IsEqual(SeamTone.SampleRate * 110 / (int)SeamTone.FrequencyHz);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ToneStartsAtZeroAndStaysWithinTheAmplitude()
    {
        var pcm = SeamTone.Pcm16();
        var peak = 0;
        for (var i = 0; i < pcm.Length / 2; i++)
            peak = Math.Max(peak, Math.Abs((int)SampleAt(pcm, i)));

        AssertThat(SampleAt(pcm, 0)).IsEqual((short)0);
        AssertBool(peak > 0 && peak <= (int)(SeamTone.Amplitude * short.MaxValue) + 1).IsTrue();
    }
}
