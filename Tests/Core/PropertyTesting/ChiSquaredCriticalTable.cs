using System;
using System.Collections.Generic;

namespace CardCleaner.Tests.Core.PropertyTesting;

/// <summary>
///     Chi-squared critical values (1-30 degrees of freedom) for the supported confidence levels.
/// </summary>
internal static class ChiSquaredCriticalTable
{
    private static readonly Dictionary<int, ChiSquaredCriticalValues> Table = new()
    {
        [1] = new ChiSquaredCriticalValues(2.706, 3.841, 6.635),
        [2] = new ChiSquaredCriticalValues(4.605, 5.991, 9.210),
        [3] = new ChiSquaredCriticalValues(6.251, 7.815, 11.345),
        [4] = new ChiSquaredCriticalValues(7.779, 9.488, 13.277),
        [5] = new ChiSquaredCriticalValues(9.236, 11.070, 15.086),
        [6] = new ChiSquaredCriticalValues(10.645, 12.592, 16.812),
        [7] = new ChiSquaredCriticalValues(12.017, 14.067, 18.475),
        [8] = new ChiSquaredCriticalValues(13.362, 15.507, 20.090),
        [9] = new ChiSquaredCriticalValues(14.684, 16.919, 21.666),
        [10] = new ChiSquaredCriticalValues(15.987, 18.307, 23.209),
        [11] = new ChiSquaredCriticalValues(17.275, 19.675, 24.725),
        [12] = new ChiSquaredCriticalValues(18.549, 21.026, 26.217),
        [13] = new ChiSquaredCriticalValues(19.812, 22.362, 27.688),
        [14] = new ChiSquaredCriticalValues(21.064, 23.685, 29.141),
        [15] = new ChiSquaredCriticalValues(22.307, 24.996, 30.578),
        [16] = new ChiSquaredCriticalValues(23.542, 26.296, 32.000),
        [17] = new ChiSquaredCriticalValues(24.769, 27.587, 33.409),
        [18] = new ChiSquaredCriticalValues(25.989, 28.869, 34.805),
        [19] = new ChiSquaredCriticalValues(27.204, 30.144, 36.191),
        [20] = new ChiSquaredCriticalValues(28.412, 31.410, 37.566),
        [21] = new ChiSquaredCriticalValues(29.615, 32.671, 38.932),
        [22] = new ChiSquaredCriticalValues(30.813, 33.924, 40.289),
        [23] = new ChiSquaredCriticalValues(32.007, 35.172, 41.638),
        [24] = new ChiSquaredCriticalValues(33.196, 36.415, 42.980),
        [25] = new ChiSquaredCriticalValues(34.382, 37.652, 44.314),
        [26] = new ChiSquaredCriticalValues(35.563, 38.885, 45.642),
        [27] = new ChiSquaredCriticalValues(36.741, 40.113, 46.963),
        [28] = new ChiSquaredCriticalValues(37.916, 41.337, 48.278),
        [29] = new ChiSquaredCriticalValues(39.087, 42.557, 49.588),
        [30] = new ChiSquaredCriticalValues(40.256, 43.773, 50.892)
    };

    public static double Lookup(int degreesOfFreedom, double confidenceLevel)
    {
        if (degreesOfFreedom < 1 || degreesOfFreedom > 30)
        {
            Assertions.AssertThat(false).OverrideFailureMessage(
                $"Chi-squared critical values not available for {degreesOfFreedom} degrees of freedom. " +
                $"Supported range: 1-30").IsTrue();
            return double.MaxValue;
        }

        var alpha = 1.0 - confidenceLevel;
        var values = Table[degreesOfFreedom];

        return alpha switch
        {
            0.10 => values.Alpha10,
            0.05 => values.Alpha05,
            0.01 => values.Alpha01,
            _ => throw new ArgumentException(
                $"Unsupported confidence level: {confidenceLevel}. " +
                $"Supported: 0.90 (α=0.10), 0.95 (α=0.05), 0.99 (α=0.01)")
        };
    }
}
