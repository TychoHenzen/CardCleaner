using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Numerics;
using Godot;

namespace CardCleaner.Tests.Core.Numerics;

[TestSuite]
[RequireGodotRuntime]
public class CoreMathTest
{
    private static readonly float[] FloatEdges = BuildFloatEdges();
    private static readonly int[] IntEdges = { int.MinValue, -1000, -3, -2, -1, 0, 1, 2, 3, 1000, int.MaxValue };

    [TestCase]
    [TestCategory("Unit")]
    public static void TestUnaryFloatMembersMatchMathf()
    {
        foreach (var x in FloatEdges)
        {
            AssertSame(Mathf.Abs(x), CoreMath.Abs(x), "Abs", x);
            AssertSame(Mathf.Floor(x), CoreMath.Floor(x), "Floor", x);
            AssertSame(Mathf.Round(x), CoreMath.Round(x), "Round", x);
            AssertSame(Mathf.Sqrt(x), CoreMath.Sqrt(x), "Sqrt", x);
            AssertSame(Mathf.Log(x), CoreMath.Log(x), "Log", x);
            AssertSame(Mathf.Tan(x), CoreMath.Tan(x), "Tan", x);
            AssertSame(Mathf.DegToRad(x), CoreMath.DegToRad(x), "DegToRad", x);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestIntegerConversionsMatchMathf()
    {
        foreach (var x in FloatEdges)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || Math.Abs(x) > 1e9f)
                continue;
            Assertions.AssertThat(CoreMath.FloorToInt(x)).OverrideFailureMessage($"FloorToInt({x})")
                .IsEqual(Mathf.FloorToInt(x));
            Assertions.AssertThat(CoreMath.CeilToInt(x)).OverrideFailureMessage($"CeilToInt({x})")
                .IsEqual(Mathf.CeilToInt(x));
            Assertions.AssertThat(CoreMath.RoundToInt(x)).OverrideFailureMessage($"RoundToInt({x})")
                .IsEqual(Mathf.RoundToInt(x));
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestIntegerMembersMatchMathf()
    {
        foreach (var a in IntEdges)
        {
            if (a != int.MinValue)
                Assertions.AssertThat(CoreMath.Abs(a)).IsEqual(Mathf.Abs(a));
            foreach (var b in IntEdges)
            {
                Assertions.AssertThat(CoreMath.Max(a, b)).IsEqual(Mathf.Max(a, b));
                Assertions.AssertThat(CoreMath.Min(a, b)).IsEqual(Mathf.Min(a, b));
                foreach (var c in IntEdges.Where(c => c >= b))
                    Assertions.AssertThat(CoreMath.Clamp(a, b, c)).IsEqual(Mathf.Clamp(a, b, c));
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestBinaryFloatMembersMatchMathf()
    {
        foreach (var a in FloatEdges)
        {
            foreach (var b in FloatEdges)
            {
                AssertSame(Mathf.Max(a, b), CoreMath.Max(a, b), "Max", a, b);
                AssertSame(Mathf.Min(a, b), CoreMath.Min(a, b), "Min", a, b);
                AssertSame(Mathf.Atan2(a, b), CoreMath.Atan2(a, b), "Atan2", a, b);
                AssertSame(Mathf.Pow(a, b), CoreMath.Pow(a, b), "Pow", a, b);
                Assertions.AssertBool(CoreMath.IsEqualApprox(a, b)).OverrideFailureMessage($"IsEqualApprox({a}, {b})")
                    .IsEqual(Mathf.IsEqualApprox(a, b));
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestTernaryFloatMembersMatchMathf()
    {
        foreach (var a in FloatEdges)
        {
            foreach (var b in FloatEdges)
            {
                foreach (var c in FloatEdges)
                {
                    if (b <= c)
                        AssertSame(Mathf.Clamp(a, b, c), CoreMath.Clamp(a, b, c), "Clamp", a, b, c);
                    AssertSame(Mathf.Lerp(a, b, c), CoreMath.Lerp(a, b, c), "Lerp", a, b, c);
                }
            }
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestApproxEqualityMatchesMathfAtItsTolerance()
    {
        foreach (var a in new[] { 0f, 1e-7f, 1e-6f, 0.5f, 1f, 3f, 1e3f, 1e6f, -1f, -1e6f })
        {
            var tolerance = Math.Max(1e-6f, 1e-6f * Math.Abs(a));
            foreach (var b in NeighboursOf(a + tolerance).Concat(NeighboursOf(a - tolerance)))
            {
                Assertions.AssertBool(CoreMath.IsEqualApprox(a, b)).OverrideFailureMessage($"IsEqualApprox({a}, {b})")
                    .IsEqual(Mathf.IsEqualApprox(a, b));
            }
        }
    }

    private static float[] BuildFloatEdges()
    {
        var values = new List<float>
        {
            0f, -0f, 0.5f, -0.5f, 1f, -1f, 1.5f, -1.5f, 2.5f, -2.5f, 3.5f, 10f, -10f, 90f, 180f, 1e-6f, 1e-7f,
            float.Epsilon, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue,
            float.MinValue, 0.1f, 123456.789f,
        };
        values.AddRange(NeighboursOf(1e-6f));
        values.AddRange(NeighboursOf(2.5f));
        return values.ToArray();
    }

    private static IEnumerable<float> NeighboursOf(float x) =>
        new[] { MathF.BitDecrement(x), x, MathF.BitIncrement(x) };

    private static void AssertSame(float expected, float actual, string name, params float[] args)
    {
        var same = float.IsNaN(expected)
            ? float.IsNaN(actual)
            : BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual);
        Assertions.AssertBool(same)
            .OverrideFailureMessage($"{name}({string.Join(", ", args)}): Mathf {expected}, CoreMath {actual}")
            .IsTrue();
    }
}
