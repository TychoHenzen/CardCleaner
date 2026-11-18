using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture graphs for the three anchor layers of the dependency gate (#240 AC-6, AC-7).</summary>
[TestSuite]
public class LayerAnchorsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void DataThatReferencesOnlyDataIsData()
    {
        var graph = new ClassGraph();
        graph.AddNode("Element", NodeRole.Data);
        graph.AddNode("Signature", NodeRole.Data);
        graph.AddEdge("Signature", "Element", EdgeKind.MemberType);

        var layers = LayerAnchors.Classify(graph);

        AssertThat(layers["Signature"].ToString()).IsEqual(nameof(Layer.Data));
        AssertThat(layers["Element"].ToString()).IsEqual(nameof(Layer.Data));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MethodFreeClassHoldingAServiceIsNotData()
    {
        var graph = new ClassGraph();
        graph.AddNode("IRandomSource", NodeRole.Interface);
        graph.AddNode("Service", NodeRole.Concrete);
        graph.AddNode("Context", NodeRole.Data);
        graph.AddEdge("IRandomSource", "Service", EdgeKind.MemberType);
        graph.AddEdge("Context", "IRandomSource", EdgeKind.MemberType);

        var layers = LayerAnchors.Classify(graph);

        AssertThat(layers["Context"].ToString()).IsEqual(nameof(Layer.Middle));
        AssertThat(layers["IRandomSource"].ToString()).IsEqual(nameof(Layer.Middle));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void InterfaceNamingOnlyDataAndContractsIsAContract()
    {
        var graph = new ClassGraph();
        graph.AddNode("Signature", NodeRole.Data);
        graph.AddNode("IReader", NodeRole.Interface);
        graph.AddNode("IWriter", NodeRole.Interface);
        graph.AddNode("Concrete", NodeRole.Concrete);
        graph.AddNode("IFactory", NodeRole.Interface);
        graph.AddEdge("IReader", "Signature", EdgeKind.MemberType);
        graph.AddEdge("IWriter", "IReader", EdgeKind.MemberType);
        graph.AddEdge("IFactory", "Concrete", EdgeKind.MemberType);

        var layers = LayerAnchors.Classify(graph);

        AssertThat(layers["IReader"].ToString()).IsEqual(nameof(Layer.Contracts));
        AssertThat(layers["IWriter"].ToString()).IsEqual(nameof(Layer.Contracts));
        AssertThat(layers["IFactory"].ToString()).IsEqual(nameof(Layer.Middle));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void InterfaceNamingUiAndUsedOnlyByUiIsAbsorbedIntoUi()
    {
        var graph = new ClassGraph();
        graph.AddNode("CardNode", NodeRole.Concrete, uiSeed: true);
        graph.AddNode("Hud", NodeRole.Concrete, uiSeed: true);
        graph.AddNode("ICardView", NodeRole.Interface);
        graph.AddNode("IShared", NodeRole.Interface);
        graph.AddNode("Rules", NodeRole.Concrete);
        graph.AddEdge("ICardView", "CardNode", EdgeKind.MemberType);
        graph.AddEdge("Hud", "ICardView", EdgeKind.MemberType);
        graph.AddEdge("IShared", "CardNode", EdgeKind.MemberType);
        graph.AddEdge("Hud", "IShared", EdgeKind.MemberType);
        graph.AddEdge("Rules", "IShared", EdgeKind.Calls);

        var layers = LayerAnchors.Classify(graph);

        AssertThat(layers["ICardView"].ToString()).IsEqual(nameof(Layer.UI));
        AssertThat(layers["IShared"].ToString()).IsEqual(nameof(Layer.Middle));
    }
}
