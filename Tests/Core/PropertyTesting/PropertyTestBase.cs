using System;
using FsCheck;
using GdUnit4;

namespace CardCleaner.Tests.Core.PropertyTesting;

/// <summary>
///     Base class for property-based tests using FsCheck with gdUnit4 integration.
///     Handles test configuration, seed capture for reproducibility, and failure reporting.
/// </summary>
/// <remarks>
///     Note: FsCheck uses its own random number generator (Rnd), separate from Godot's
///     RandomNumberGenerator. For FsCheck property test reproduction, use the seed reported
///     in failure messages with the WithSeed() method. For Godot RNG-based tests, use SeedManager.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public abstract class PropertyTestBase
{
    private const int DefaultIterations = 100;
    private const int DefaultMaxShrinks = 100;

    [BeforeTest]
    public void SetupPropertyTest() => SeedManager.Reset();

    /// <summary>
    ///     Configures and runs a property test using a fluent API.
    /// </summary>
    /// <example>
    ///     Property(p => p
    ///     .ForAll&lt;int&gt;(x => x + 0 == x)
    ///     .Iterations(1000)
    ///     .WithSeed(12345)); // For reproduction
    /// </example>
    protected void Property(Action<PropertyTestRunner> configure)
    {
        var runner = new PropertyTestRunner();
        configure(runner);
        runner.Execute();
    }

    /// <summary>
    ///     Fluent API for configuring and executing property tests.
    /// </summary>
    public class PropertyTestRunner
    {
        private int _iterations = DefaultIterations;
        private int _maxShrinks = DefaultMaxShrinks;
        private Property? _property;
        private int? _seed;

        /// <summary>
        ///     Sets the number of test iterations. Default is 100.
        /// </summary>
        public PropertyTestRunner Iterations(int count)
        {
            if (count <= 0)
                throw new ArgumentException("Iteration count must be positive", nameof(count));
            _iterations = count;
            return this;
        }

        /// <summary>
        ///     Sets the maximum number of shrink attempts when finding minimal counterexamples.
        ///     Default is 100. Set to 0 to disable shrinking.
        /// </summary>
        public PropertyTestRunner MaxShrinks(int count)
        {
            if (count < 0)
                throw new ArgumentException("Max shrinks cannot be negative", nameof(count));
            _maxShrinks = count;
            return this;
        }

        /// <summary>
        ///     Sets an explicit seed for reproducible test runs.
        ///     Use this with the seed from a failure message to reproduce a specific counterexample.
        /// </summary>
        public PropertyTestRunner WithSeed(int seed)
        {
            _seed = seed;
            return this;
        }

        /// <summary>
        ///     Defines a single-parameter property using FsCheck's default generator.
        /// </summary>
        public PropertyTestRunner ForAll<T>(Func<T, bool> property)
        {
            _property = Prop.ForAll(property);
            return this;
        }

        /// <summary>
        ///     Defines a single-parameter property with a custom arbitrary generator.
        /// </summary>
        public PropertyTestRunner ForAll<T>(Arbitrary<T> arbitrary, Func<T, bool> property)
        {
            _property = Prop.ForAll(arbitrary, property);
            return this;
        }

        /// <summary>
        ///     Defines a two-parameter property using FsCheck's default generators.
        /// </summary>
        public PropertyTestRunner ForAll<T1, T2>(Func<T1, T2, bool> property)
        {
            _property = Prop.ForAll(property);
            return this;
        }

        /// <summary>
        ///     Defines a three-parameter property using FsCheck's default generators.
        /// </summary>
        public PropertyTestRunner ForAll<T1, T2, T3>(Func<T1, T2, T3, bool> property)
        {
            _property = Prop.ForAll(property);
            return this;
        }

        /// <summary>
        ///     Uses a pre-built FsCheck Property for advanced scenarios like Prop.Implies or Prop.Classify.
        /// </summary>
        public PropertyTestRunner Check(Property property)
        {
            _property = property;
            return this;
        }

        internal void Execute()
        {
            if (_property == null)
                throw new InvalidOperationException("No property defined. Call ForAll or Check before executing.");

            var actualSeed = _seed ?? GenerateSeed();

            var config = Config.Quick
                .WithMaxTest(_iterations)
                .WithMaxRejected(_iterations * 10)
                .WithReplay(unchecked((ulong)(uint)actualSeed), 1UL);

            try
            {
                _property.Check(config);
            }
            catch (Exception ex) when (ex.Message.Contains("Falsifiable"))
            {
                // FsCheck throws on property failure - capture and format nicely
                ReportFailure(ex, actualSeed);
            }
        }

        private void ReportFailure(Exception fsCheckException, int seed)
        {
            var message = $"Property test failed\n" +
                          $"Configuration: {_iterations} iterations, {_maxShrinks} max shrinks\n" +
                          $"FsCheck seed for reproduction: {seed}\n" +
                          $"To reproduce: .WithSeed({seed})\n\n" +
                          $"{fsCheckException.Message}";

            Assertions.AssertThat(false)
                .OverrideFailureMessage(message)
                .IsTrue();
        }

        private static int GenerateSeed() => (int)(DateTime.UtcNow.Ticks % int.MaxValue);
    }
}
