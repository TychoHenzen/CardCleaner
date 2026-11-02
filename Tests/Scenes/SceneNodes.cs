using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Node-tree queries shared by the scene suites: every descendant, a required child, the visible visuals
/// under a node and their footprint on the ground plane.
/// </summary>
public static class SceneNodes
{
    /// <summary>
    /// Every descendant, including runtime-loaded pack art. That art has no owner, so owned must be false
    /// for the search to see it.
    /// </summary>
    public static IEnumerable<Node> Descendants(Node root) =>
        root.FindChildren("*", string.Empty, true, false);

    public static Node3D Require(Node root, string path)
    {
        var node = root.GetNodeOrNull<Node3D>(path);
        AssertThat(node).OverrideFailureMessage($"{path} is missing under {root.GetPath()}").IsNotNull();
        return node!;
    }

    /// <summary>Visuals that are visible in the tree; hidden placeholders are skipped.</summary>
    public static IEnumerable<VisualInstance3D> VisibleVisuals(Node root) =>
        Descendants(root).OfType<VisualInstance3D>().Where(visual => visual.IsVisibleInTree());

    public static Rect2 Footprint(VisualInstance3D visual)
    {
        var box = visual.GlobalTransform * visual.GetAabb();
        return new Rect2(box.Position.X, box.Position.Z, box.Size.X, box.Size.Z);
    }
}
