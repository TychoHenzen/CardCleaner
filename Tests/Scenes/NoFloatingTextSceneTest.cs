using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Signage;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     Text in the world is printed on something, as it is in a real shop: a sign board, a price tag, a card. No
///     shipped scene holds a label that turns to face the camera and floats in the air, and every sign board's text
///     fits on its board. The debug scenes are tools, not the game, so they are left out.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NoFloatingTextSceneTest
{
    private const string ScenesRoot = "res://Scenes";
    private const string DebugScenes = "res://Scenes/Debug/";

    [TestCase]
    [TestCategory("Unit")]
    public static void NoShippedSceneHoldsALabelThatFacesTheCamera()
    {
        var floating = Collect(root => Descendants(root).OfType<Label3D>()
            .Where(label => label.Billboard != BaseMaterial3D.BillboardModeEnum.Disabled)
            .Select(label => root.GetPathTo(label).ToString()));

        AssertThat(string.Join(", ", floating)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EverySignBoardFitsItsTextOnTheBoard()
    {
        var signs = Collect(root => Descendants(root).OfType<SignBoard>().Select(sign => sign.Name.ToString()));
        var overflowing = Collect(root => Descendants(root).OfType<SignBoard>()
            .Where(sign => !sign.TextFits)
            .Select(sign => $"{root.GetPathTo(sign)} \"{sign.Text}\""));

        AssertThat(signs.Count).IsGreater(0);
        AssertThat(string.Join(", ", overflowing)).IsEmpty();
    }

    // What the probe finds in each shipped scene, prefixed with the scene. A scene is instantiated but never added to
    // the tree, so what is read is what the file stores.
    private static List<string> Collect(System.Func<Node, IEnumerable<string>> probe)
    {
        var scenes = SceneFiles.Under(ScenesRoot).Where(path => !path.StartsWith(DebugScenes)).ToList();
        AssertThat(scenes.Count).IsGreater(0);

        var found = new List<string>();
        foreach (var scenePath in scenes)
        {
            var root = GD.Load<PackedScene>(scenePath).Instantiate<Node>();
            found.AddRange(probe(root).Select(item => $"{scenePath}: {item}"));
            root.Free();
        }

        return found;
    }

    private static IEnumerable<Node> Descendants(Node node) =>
        node.GetChildren(true).SelectMany(child => Descendants(child).Prepend(child));
}
