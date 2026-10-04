using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.CompiledResolver;

/// <summary>
///     CompiledResolverTransitionTest scenarios split out of CompiledTransitionResolverTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompiledResolverTransitionTest : CompiledTransitionResolverTestBase
{
    // ==================== Resolver Creation ====================

    [TestCase]
    public void TestResolverCreatesSuccessfully()
    {
        AssertThat(_resolver).IsNotNull();
        AssertThat(_resolver.CompiledAtlasSourceId).IsEqual(0);
    }

    // ==================== Direct Transition Lookup ====================

    [TestCase]
    public void TestResolveTransitionReturnsValidCoordsForKnownPair()
    {
        // Get first transition key from the map
        var firstKey = _transitionMap.Transitions.Keys.FirstOrDefault();
        if (firstKey == null)
        {
            GD.PrintErr("No transitions in map - test cannot run");
            return;
        }

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);
        var entry = _transitionMap.Transitions[firstKey];

        // Find first non-null variant
        var bitmask = -1;
        for (var i = 0; i < entry.Variants.Length; i++)
        {
            if (entry.Variants[i] != null && entry.Variants[i]!.Length > 0)
            {
                bitmask = i;
                break;
            }
        }

        if (bitmask < 0)
        {
            GD.Print($"Transition {firstKey} has no non-null variants");
            return;
        }

        var coords = _resolver.ResolveTransition(borderId, outerTerrain, bitmask);

        AssertThat(coords).IsNotNull();
        AssertThat(coords!.Value.X).IsEqual(entry.Variants[bitmask]![0].X);
        AssertThat(coords!.Value.Y).IsEqual(entry.Variants[bitmask]![0].Y);
    }

    [TestCase]
    public void TestResolveTransitionReturnsNullForUnknownPair()
    {
        var coords = _resolver.ResolveTransition("nonexistent_terrain_xyz", "another_fake_terrain", 5);
        AssertThat(coords).IsNull();
    }

    [TestCase]
    public void TestResolveTransitionReturnsNullForInvalidBitmask()
    {
        // Get a known valid pair first
        var firstKey = _transitionMap.Transitions.Keys.FirstOrDefault();
        if (firstKey == null) return;

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);

        // Try with bitmask -1 (invalid)
        var coords = _resolver.ResolveTransition(borderId, outerTerrain, -1);
        AssertThat(coords).IsNull();
    }

    // ==================== All Bitmask Values ====================

    [TestCase]
    public void TestResolveTransitionHandlesAllBitmaskValues0To15()
    {
        // Get first corner16 transition
        var corner16Key = _transitionMap.Transitions
            .FirstOrDefault(kvp => kvp.Value.Format == "corner16").Key;

        if (corner16Key == null)
        {
            GD.PrintErr("No corner16 transitions found");
            return;
        }

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(corner16Key);
        var entry = _transitionMap.Transitions[corner16Key];

        var results = new System.Collections.Generic.List<string>();

        for (var bitmask = 0; bitmask < 16; bitmask++)
        {
            var coords = _resolver.ResolveTransition(borderId, outerTerrain, bitmask);
            var expectedVariant = bitmask < entry.Variants.Length ? entry.Variants[bitmask] : null;

            if (expectedVariant != null && expectedVariant.Length > 0)
            {
                var firstVariant = expectedVariant[0];
                if (coords == null)
                    results.Add($"bitmask {bitmask}: expected ({firstVariant.X},{firstVariant.Y}), got null");
                else if (coords.Value.X != firstVariant.X || coords.Value.Y != firstVariant.Y)
                    results.Add(
                        $"bitmask {bitmask}: expected ({firstVariant.X},{firstVariant.Y}), " +
                        $"got ({coords.Value.X},{coords.Value.Y})");
            }
            else
            {
                // Null expected - coords can be null or any value
            }
        }

        if (results.Count > 0)
            GD.PrintErr($"Bitmask lookup failures for {corner16Key}:\n{string.Join("\n", results)}");

        AssertThat(results.Count).IsEqual(0);
    }

    // ==================== HasTransition ====================

    [TestCase]
    public void TestHasTransitionReturnsTrueForKnownPair()
    {
        var firstKey = _transitionMap.Transitions.Keys.FirstOrDefault();
        if (firstKey == null) return;

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);

        AssertBool(_resolver.HasTransition(borderId, outerTerrain)).IsTrue();
    }

    [TestCase]
    public void TestHasTransitionReturnsFalseForUnknownPair()
    {
        AssertBool(_resolver.HasTransition("fake_terrain_123", "fake_terrain_456")).IsFalse();
    }
}
