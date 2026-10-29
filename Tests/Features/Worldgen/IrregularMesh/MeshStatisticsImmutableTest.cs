using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

[TestSuite]
[RequireGodotRuntime]
public class MeshStatisticsImmutableTest
{
    private const BindingFlags AllInstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [TestCase]
    [TestCategory("Unit")]
    public static void OfMeasuresAFixedMeshWithTheSameCounts()
    {
        // Four vertices (two on the boundary) and two quads with areas 1 and 3.
        var vertices = new List<MeshVertex>
        {
            new(0, new Vector2(0, 0)) { IsBoundary = true },
            new(1, new Vector2(1, 0)) { IsBoundary = true },
            new(2, new Vector2(1, 1)),
            new(3, new Vector2(0, 1)),
        };
        var areas = new List<float> { 1f, 3f };
        var bounds = (new Vector2(0, 0), new Vector2(1, 1));

        var statistics = MeshStatistics.Of(vertices, areas, bounds);

        AssertInt(statistics.VertexCount).IsEqual(4);
        AssertInt(statistics.QuadCount).IsEqual(2);
        AssertInt(statistics.BoundaryVertexCount).IsEqual(2);
        AssertFloat(statistics.MinArea).IsEqual(1f);
        AssertFloat(statistics.MaxArea).IsEqual(3f);
        AssertFloat((float)statistics.AvgArea).IsEqual(2f);
        AssertFloat(statistics.AreaStdDev).IsEqual(1f);
        AssertThat(statistics.Bounds.Min).IsEqual(new Vector2(0, 0));
        AssertThat(statistics.Bounds.Max).IsEqual(new Vector2(1, 1));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryWritableMemberIsInitOnly()
    {
        var properties = typeof(MeshStatistics).GetProperties(AllInstanceMembers);
        AssertBool(properties.Length > 0).IsTrue();

        foreach (var property in properties)
        {
            if (!property.CanWrite)
            {
                continue;
            }

            var modifiers = property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers();
            AssertBool(System.Array.IndexOf(modifiers, typeof(IsExternalInit)) >= 0)
                .OverrideFailureMessage($"{property.Name} has a settable setter")
                .IsTrue();
        }

        foreach (var field in typeof(MeshStatistics).GetFields(AllInstanceMembers))
        {
            AssertBool(field.IsInitOnly).OverrideFailureMessage($"{field.Name} is a mutable field").IsTrue();
        }
    }
}
