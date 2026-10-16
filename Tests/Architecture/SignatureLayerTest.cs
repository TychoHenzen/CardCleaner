using System.Collections.Generic;
using System.Linq;
using CardCleaner.Tests.Architecture.DependencyGate;
using Godot;

namespace CardCleaner.Tests.Architecture;

/// <summary>#278 AC-1: the layered gate classifies CardSignature as pure data and names only Element.</summary>
[TestSuite]
[RequireGodotRuntime]
public class SignatureLayerTest
{
    [TestCase(Timeout = 170000)]
    [TestCategory("Unit")]
    public static void CardSignatureIsPureDataAndNamesOnlyElement()
    {
        var root = ProjectSettings.GlobalizePath("res://");
        var extracted = ClassGraphExtractor.Extract(
            RepositorySources.Scripts(root), RepositorySources.Addons(root), CompilationReferences.FromHost());
        var graph = extracted.Graph;
        var layers = LayerAnchors.Classify(graph);
        var signature = IdOf(layers.Keys, "CardSignature");

        AssertThat(layers[signature].ToString()).IsEqual(nameof(Layer.Data));
        var references = LayerAnchors.References(graph)[signature].Select(ShortName).OrderBy(name => name).ToArray();
        AssertThat(references).IsEqual(new[] { "Element" });
        AssertThat(layers[IdOf(layers.Keys, "CardSignatureHash")].ToString()).IsNotEqual(nameof(Layer.Data));
    }

    // ASSUMPTION: graph node ids are bare class names (the probe showed "CardSignature", not a dotted full name);
    // Single still throws on a duplicate short name, so the lookup stays unambiguous.
    private static string IdOf(IEnumerable<string> ids, string name) =>
        ids.Single(id => ShortName(id) == name);

    private static string ShortName(string id) => id.Substring(id.LastIndexOf('.') + 1);
}
