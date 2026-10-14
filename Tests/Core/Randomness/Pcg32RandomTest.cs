using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Core.Randomness;

[TestSuite]
[RequireGodotRuntime]
public class Pcg32RandomTest
{
    private static readonly ulong[] Seeds = [0, 1, 777, 4242, 9223372036854775813UL];

    [TestCase]
    [TestCategory("Unit")]
    public static void MembersMatchGodotBitForBit()
    {
        foreach (var seed in Seeds)
        {
            Compare("Randi", seed, random => random.Randi(), random => random.Randi());
            Compare("Randf", seed, random => BitConverter.SingleToInt32Bits(random.Randf()), random => BitConverter.SingleToInt32Bits(random.Randf()));
            Compare("RandiRange", seed, random => random.RandiRange(-5, 9), random => random.RandiRange(-5, 9));
            Compare("RandiRangeFull", seed, random => random.RandiRange(int.MinValue, int.MaxValue), random => random.RandiRange(int.MinValue, int.MaxValue));
            Compare("RandfRange", seed, random => BitConverter.SingleToInt32Bits(random.RandfRange(-2.5f, 7f)), random => BitConverter.SingleToInt32Bits(random.RandfRange(-2.5f, 7f)));
            Compare("Randfn", seed, random => BitConverter.SingleToInt32Bits(random.Randfn(0f, 1f)), random => BitConverter.SingleToInt32Bits(random.Randfn(0f, 1f)));
        }
    }

    private static void Compare<T>(
        string member,
        ulong seed,
        Func<IRandom, T> actualDraw,
        Func<RandomNumberGenerator, T> expectedDraw)
    {
        var actual = RandomSources.Create(seed);
        var expected = new RandomNumberGenerator { Seed = seed };
        for (var draw = 0; draw < 1000; draw++)
        {
            var actualValue = actualDraw(actual);
            var expectedValue = expectedDraw(expected);
            if (!EqualityComparer<T>.Default.Equals(actualValue, expectedValue))
                throw new InvalidOperationException($"{member} seed {seed} draw {draw}: expected {expectedValue}, actual {actualValue}");
        }
    }
}
