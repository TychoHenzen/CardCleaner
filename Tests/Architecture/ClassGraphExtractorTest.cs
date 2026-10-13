using System.Linq;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture sources for the Roslyn class-graph extraction of the dependency gate (#240 AC-6, AC-7).</summary>
[TestSuite]
[RequireGodotRuntime]
public class ClassGraphExtractorTest
{
    private const string Source = """
        namespace Fixture.Shapes;

        public enum Suit { Hearts, Spades }

        public record CardFace(Suit Suit, int Rank);

        public interface IRandomSource { int Next(); }

        public class DrawContext
        {
            public IRandomSource Random = null!;
        }

        public class Outer
        {
            public class Inner { }
        }

        public class User
        {
            public Outer.Inner Field = new();
        }
        """;

    [TestCase]
    [TestCategory("Unit")]
    public static void NestedTypesFoldIntoTheirOuterType()
    {
        var graph = Extract();

        AssertThat(graph.Nodes.ContainsKey("Inner")).IsFalse();
        AssertThat(graph.Nodes.ContainsKey("Outer")).IsTrue();
        AssertThat(graph.Edges.ContainsKey(new EdgeKey("User", "Outer"))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RecordReferencingOnlyEnumsIsData()
    {
        var graph = Extract();
        var layers = LayerAnchors.Classify(graph);

        AssertThat(graph.Nodes["CardFace"].Role.ToString()).IsEqual(nameof(NodeRole.Data));
        AssertThat(layers["CardFace"].ToString()).IsEqual(nameof(Layer.Data));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MethodFreeClassHoldingAnInterfaceServiceIsNotData()
    {
        var graph = Extract();
        var layers = LayerAnchors.Classify(graph);

        AssertThat(graph.Nodes["DrawContext"].Role.ToString()).IsEqual(nameof(NodeRole.Data));
        AssertThat(layers["IRandomSource"].ToString()).IsEqual(nameof(Layer.Contracts));
        AssertThat(layers["DrawContext"].ToString()).IsEqual(nameof(Layer.Middle));
    }

    private static ClassGraph Extract() =>
        ClassGraphExtractor.Extract([new SourceFile("Scripts/Features/Fixture/Shapes.cs", Source)], [],
            CompilationReferences.FromHost()).Graph;
}
