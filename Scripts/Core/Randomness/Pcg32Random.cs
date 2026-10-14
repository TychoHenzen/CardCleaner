using System;
using System.Numerics;
using System.Runtime.InteropServices;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Randomness;

public sealed class Pcg32Random : IRandom
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong DefaultIncrement = 1442695040888963407UL;
    private const float ComparisonEpsilon = 0.00001f;

    private ulong _seed;
    private ulong _state;
    private readonly ulong _increment = unchecked((DefaultIncrement << 1) | 1);

    public Pcg32Random()
    {
        Randomize();
    }

    public Pcg32Random(ulong seed)
    {
        Seed = seed;
    }

    public ulong Seed
    {
        get => _seed;
        set
        {
            _seed = value;
            _state = 0;
            Randi();
            _state = unchecked(_state + value);
            Randi();
        }
    }

    public uint Randi()
    {
        var oldState = _state;
        _state = unchecked(oldState * Multiplier + _increment);
        var xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotation = (int)(oldState >> 59);
        return BitOperations.RotateRight(xorshifted, rotation);
    }

    public float Randf()
    {
        var exponentSource = Randi();
        if (exponentSource == 0)
            return 0f;

        var significand = (float)(Randi() | 0x80000001U);
        return MathF.ScaleB(significand, -32 - BitOperations.LeadingZeroCount(exponentSource));
    }

    public int RandiRange(int minimum, int maximum)
    {
        var lowerBound = Math.Min(minimum, maximum);
        var upperBound = Math.Max(minimum, maximum);
        if (lowerBound == upperBound)
            return lowerBound;

        var difference = unchecked((uint)(upperBound - lowerBound));
        var offset = difference == uint.MaxValue ? Randi() : BoundedRandi(difference + 1);
        return unchecked((int)(offset + (uint)lowerBound));
    }

    public float RandfRange(float minimum, float maximum) => Randf() * (maximum - minimum) + minimum;

    public float Randfn(float mean, float deviation)
    {
        var magnitudeSource = Randf();
        if (magnitudeSource < ComparisonEpsilon)
            magnitudeSource += ComparisonEpsilon;
        return (float)(mean + deviation *
            (Cos((float)Math.Tau * Randf()) * Sqrt(-2.0 * Log(magnitudeSource))));
    }

    public void Randomize()
    {
        Seed = unchecked((ulong)DateTime.UtcNow.Ticks ^ (ulong)Environment.TickCount64);
    }

    private uint BoundedRandi(uint bound)
    {
        var threshold = unchecked(0U - bound) % bound;
        uint value;
        do
        {
            value = Randi();
        } while (value < threshold);

        return value % bound;
    }

    private static float Cos(float value) => OperatingSystem.IsWindows() ? WindowsCos(value) : MathF.Cos(value);

    private static float Log(float value) => OperatingSystem.IsWindows() ? WindowsLog(value) : MathF.Log(value);

    private static double Sqrt(double value) => OperatingSystem.IsWindows() ? WindowsSqrt(value) : Math.Sqrt(value);

    [DllImport("msvcrt.dll", EntryPoint = "cosf", CallingConvention = CallingConvention.Cdecl)]
    private static extern float WindowsCos(float value);

    [DllImport("msvcrt.dll", EntryPoint = "logf", CallingConvention = CallingConvention.Cdecl)]
    private static extern float WindowsLog(float value);

    [DllImport("msvcrt.dll", EntryPoint = "sqrt", CallingConvention = CallingConvention.Cdecl)]
    private static extern double WindowsSqrt(double value);
}
