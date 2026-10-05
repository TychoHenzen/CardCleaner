using System;

namespace CardCleaner.Scripts.Features.Portal.Models;

/// <summary>Placeholder seam hum, generated rather than imported: a quiet sine tone as 16-bit mono PCM.</summary>
public static class SeamTone
{
    public const int SampleRate = 22050;
    public const float FrequencyHz = 220f;
    public const float Amplitude = 0.3f;

    /// <summary>
    ///     One whole number of periods, so a looping stream has no click at the loop point.
    /// </summary>
    public static byte[] Pcm16(int sampleRate = SampleRate, float frequencyHz = FrequencyHz, int periods = 110)
    {
        var samples = (int)MathF.Round(periods * sampleRate / frequencyHz);
        var bytes = new byte[samples * sizeof(short)];
        for (var i = 0; i < samples; i++)
        {
            var value = MathF.Sin(2f * MathF.PI * frequencyHz * i / sampleRate) * Amplitude;
            var sample = (short)MathF.Round(value * short.MaxValue);
            bytes[i * 2] = (byte)(sample & 0xFF);
            bytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return bytes;
    }
}
