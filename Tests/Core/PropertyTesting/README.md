# Property-Based Testing Framework

This directory contains the property-based testing infrastructure for CardCleaner, built on FsCheck with gdUnit4 integration.

## Overview

Property-based testing validates that code maintains invariants across many randomly-generated inputs, rather than testing specific examples. This is valuable for procedural/random systems where exhaustive example testing is impractical.

## Quick Start

```csharp
[TestSuite]
[RequireGodotRuntime]
public class MyProperties : PropertyTestBase
{
    [TestCase]
    public void MyPropertyTest()
    {
        Property(p => p
            .ForAll<int>(x => x + 0 == x)
            .Iterations(1000));
    }
}
```

## Components

### PropertyTestBase
Base class for property tests. Provides:
- FsCheck/gdUnit4 integration
- Seed tracking for reproduction
- Fluent configuration API

```csharp
Property(p => p
    .ForAll(CardSignatureArbitrary.Default, sig => sig.Elements.All(e => e >= -1 && e <= 1))
    .Iterations(500)
    .MaxShrinks(100)
    .WithSeed(12345)); // For reproducing a failure
```

### Generators

#### CardSignatureArbitrary
Generates valid 8-dimensional CardSignature instances.

```csharp
// Register before tests
CardSignatureArbitrary.Register();

// Use in properties
Property(p => p.ForAll(CardSignatureArbitrary.Default, sig => ...));

// Specialized generators
CardSignatureGenerators.Extreme     // All ±1 values
CardSignatureGenerators.Sparse      // Mostly zeros
CardSignatureGenerators.InRange(-0.5f, 0.5f)  // Restricted range
CardSignatureGenerators.Pair        // Two signatures for testing binary ops
```

#### GradientArbitraries
Generators for gradient system testing.

```csharp
GradientArbitraries.Register();

GradientArbitraries.RadialGradient      // Random RadialGradient
GradientArbitraries.NoiseGradient       // Random NoiseGradient
GradientArbitraries.PositionWithMapSize // Valid (position, mapSize) pairs
GradientArbitraries.BlendFactor         // Float in [0, 1]
```

### SeedManager
Tracks random seeds for Godot RNG (separate from FsCheck's RNG).

```csharp
// Create seeded RNG for Godot-based randomness
var rng = SeedManager.CreateRng();
// or with explicit seed for reproduction:
var rng = SeedManager.CreateRng(42);

// Get reproduction info for failure messages
SeedManager.GetReproductionInfo(); // "To reproduce, use seed: 12345UL"
```

### StatisticalAssertions
Validate distributions and statistical properties.

```csharp
// Uniform distribution (chi-squared test)
StatisticalAssertions.AssertUniformDistribution(samples);

// Weighted distribution
StatisticalAssertions.AssertWeightedDistribution(samples,
    new Dictionary<string, double> { ["common"] = 9, ["rare"] = 1 });

// Continuous distributions
StatisticalAssertions.AssertMean(samples, expectedMean: 0.0, tolerance: 0.1);
StatisticalAssertions.AssertVariance(samples, expectedVariance: 1.0, tolerance: 0.2);
StatisticalAssertions.AssertStandardDeviation(samples, expectedStdDev: 1.0, tolerance: 0.1);

// Bounds checking
StatisticalAssertions.AssertAllInBounds(samples, min: -1.0, max: 1.0);
```

## Creating New Generators

### Basic Pattern
```csharp
public static class MyTypeArbitrary
{
    public static Gen<MyType> Generator =>
        from value1 in Gen.Choose(0, 100)
        from value2 in Arb.Default.String().Generator
        select new MyType(value1, value2);

    public static IEnumerable<MyType> Shrinker(MyType instance)
    {
        // Yield simpler versions for minimal counterexamples
        yield return new MyType(0, "");
        yield return new MyType(instance.Value1 / 2, instance.Value2);
    }

    public static Arbitrary<MyType> Default =>
        Arb.From(Generator, Shrinker);

    public static void Register()
    {
        Arb.Register<MyTypeArbitraries>();
    }

    private class MyTypeArbitraries
    {
        public static Arbitrary<MyType> MyType => Default;
    }
}
```

### Generator Combinators
```csharp
Gen.Choose(min, max)              // Random int in range
Gen.Elements(a, b, c)             // Random element from list
Gen.ArrayOf(n, gen)               // Array of n elements
Gen.OneOf(gen1, gen2)             // One of several generators
Gen.Frequency((3, gen1), (1, gen2)) // Weighted selection
```

## Writing Property Tests

### Common Patterns

**Invariants**: Properties that must always hold
```csharp
.ForAll<CardSignature>(sig => sig.Elements.All(e => e >= -1 && e <= 1))
```

**Symmetry**: Operations that should be symmetric
```csharp
.ForAll(CardSignatureGenerators.Pair, pair =>
    pair.A.DistanceTo(pair.B) == pair.B.DistanceTo(pair.A))
```

**Roundtrip**: Inverse operations cancel out
```csharp
.ForAll<string>(s => Deserialize(Serialize(s)) == s)
```

**Determinism**: Same inputs produce same outputs
```csharp
.ForAll<int>(seed => {
    var result1 = GenerateWithSeed(seed);
    var result2 = GenerateWithSeed(seed);
    return result1.Equals(result2);
})
```

**Triangle Inequality**: For distance-like functions
```csharp
.ForAll((a, b, c) => a.DistanceTo(c) <= a.DistanceTo(b) + b.DistanceTo(c))
```

### Multi-Parameter Properties
```csharp
Property(p => p.ForAll<int, string>((i, s) => SomeProperty(i, s)));

// With custom arbitraries
Property(p => p.ForAll(
    CardSignatureArbitrary.Default,
    Arb.From(GradientArbitraries.RadialGradient),
    (sig, gradient) => SomeProperty(sig, gradient)));
```

## Debugging Failed Properties

When a property fails, FsCheck:
1. Reports the counterexample
2. Shrinks to find a minimal failing case
3. Reports the seed for reproduction

### Reproducing a Failure
```csharp
[TestCase]
public void ReproduceFailure()
{
    Property(p => p
        .ForAll(MyArbitrary.Default, x => MyProperty(x))
        .WithSeed(12345)); // Use seed from failure message
}
```

### Increasing Visibility
```csharp
Property(p => p
    .ForAll(MyArbitrary.Default, x => {
        GD.Print($"Testing with: {x}"); // Log each test case
        return MyProperty(x);
    })
    .Iterations(10)); // Reduce iterations for debugging
```

### Disabling Shrinking
```csharp
Property(p => p
    .ForAll(MyArbitrary.Default, x => MyProperty(x))
    .MaxShrinks(0)); // See original counterexample
```

## Best Practices

1. **Start with 100 iterations**, increase for critical properties
2. **Register arbitraries in `[BeforeTest]`** to ensure they're available
3. **Use `Epsilon` for float comparisons** (typically 0.0001f)
4. **Write focused properties** - one invariant per test
5. **Name properties descriptively** - they document the system
6. **Consider edge cases** - use `Gen.Frequency` to ensure coverage

## File Structure

```
Tests/Core/PropertyTesting/
├── PropertyTestBase.cs       # Base class for property tests
├── SeedManager.cs           # Godot RNG seed tracking
├── StatisticalAssertions.cs # Distribution validators
├── Generators/
│   ├── CardSignatureArbitrary.cs
│   └── GradientArbitraries.cs
└── Properties/
    └── (domain property tests live with their features)

Tests/Features/*/Properties/   # Feature-specific property tests
```

## References

- [FsCheck Documentation](https://fscheck.github.io/FsCheck/)
- [Property-Based Testing with FsCheck](https://fsharpforfunandprofit.com/posts/property-based-testing/)
- [Choosing Properties](https://fsharpforfunandprofit.com/posts/property-based-testing-2/)
