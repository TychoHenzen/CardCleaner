using System;

namespace CardCleaner.Scripts.Core.Numerics;

/// <summary>
/// Engine-free math. Each member copies the body of the Godot.Mathf overload of the same signature
/// (GodotSharp 4.7.1), so results are identical; CoreMathTest compares them.
/// </summary>
public static class CoreMath
{
    private const float Epsilon = 1e-06f;
    private const float DegToRadFactor = 0.0174532924f;

    public static float Abs(float s) => MathF.Abs(s);

    public static int Abs(int s) => Math.Abs(s);

    public static float Atan2(float y, float x) => MathF.Atan2(y, x);

    public static int CeilToInt(float s) => (int)MathF.Ceiling(s);

    public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);

    public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);

    public static float DegToRad(float deg) => deg * DegToRadFactor;

    public static float Floor(float s) => MathF.Floor(s);

    public static int FloorToInt(float s) => (int)MathF.Floor(s);

    public static bool IsEqualApprox(float a, float b)
    {
        if (a == b)
            return true;
        var tolerance = Epsilon * MathF.Abs(a);
        if (tolerance < Epsilon)
            tolerance = Epsilon;
        return MathF.Abs(a - b) < tolerance;
    }

    public static float Lerp(float from, float to, float weight) => from + (to - from) * weight;

    public static float Log(float s) => MathF.Log(s);

    public static float Max(float a, float b) => MathF.Max(a, b);

    public static int Max(int a, int b) => Math.Max(a, b);

    public static float Min(float a, float b) => MathF.Min(a, b);

    public static int Min(int a, int b) => Math.Min(a, b);

    public static float Pow(float x, float y) => MathF.Pow(x, y);

    public static float Round(float s) => MathF.Round(s);

    public static int RoundToInt(float s) => (int)MathF.Round(s);

    public static float Sqrt(float s) => MathF.Sqrt(s);

    public static float Tan(float s) => MathF.Tan(s);
}
