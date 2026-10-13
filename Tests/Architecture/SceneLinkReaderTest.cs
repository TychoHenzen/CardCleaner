using System;
using System.Collections.Generic;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture scene for the NodePath links of the dependency gate (#240 AC-7).</summary>
[TestSuite]
public class SceneLinkReaderTest
{
    private const string Scene = """
        [gd_scene load_steps=3 format=3]

        [ext_resource type="Script" path="res://Scripts/Hud.cs" id="1_hud"]
        [ext_resource type="Script" path="res://Scripts/Meter.cs" id="2_meter"]

        [node name="Hud" type="Control"]
        script = ExtResource("1_hud")
        MeterPath = NodePath("Bars/Meter")
        LabelPath = NodePath("Bars/Label")

        [node name="Bars" type="Control" parent="."]

        [node name="Meter" type="Control" parent="Bars"]
        script = ExtResource("2_meter")
        HudPath = NodePath("../..")

        [node name="Label" type="Label" parent="Bars"]
        """;

    [TestCase]
    [TestCategory("Unit")]
    public static void NodePathBetweenTwoScriptedNodesIsOneLink()
    {
        var classes = new Dictionary<string, IReadOnlyList<string>>
        {
            ["Scripts/Hud.cs"] = ["Hud"],
            ["Scripts/Meter.cs"] = ["Meter"],
        };

        var links = SceneLinkReader.Read([new SourceFile("Scenes/Hud.tscn", Scene)], classes);

        AssertThat(links.Count).IsEqual(2);
        AssertThat(links[0].ToString()).IsEqual(new SceneLinkReader.Link("Hud", "Meter", 1).ToString());
        AssertThat(links[1].ToString()).IsEqual(new SceneLinkReader.Link("Meter", "Hud", 1).ToString());
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SceneLinkAndCodeReferenceOnOnePairMergeIntoOneMemberTypeCount()
    {
        var graph = new ClassGraph();
        graph.AddNode("Hud", NodeRole.Concrete);
        graph.AddNode("Meter", NodeRole.Concrete);

        graph.AddEdge("Hud", "Meter", EdgeKind.MemberType, 3);
        graph.AddEdge("Hud", "Meter", EdgeKind.MemberType, 2);
        graph.AddEdge("Hud", "Meter", EdgeKind.Calls);

        var weight = graph.Edges[new EdgeKey("Hud", "Meter")];
        AssertThat(weight.Weight).IsEqual(11L);
        AssertThat(Math.Abs(weight.ClusterWeight - (2 * (1 + Math.Log2(5)) + 1)) < 1e-9).IsTrue();
    }
}
