using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class StockItemLookTest
{
    private const float Tolerance = 0.0001f;
    private static readonly Vector3 CardSize = new(0.32f, 0.45f, 0.005f);

    [TestCase]
    [TestCategory("Unit")]
    public static void OnlyVisibleMeshesAreCopied()
    {
        var body = new Node3D();
        body.AddChild(Mesh(new Vector3(0.3f, 0.2f, 0.25f)));
        body.AddChild(Hidden(Mesh(Vector3.One)));
        var hiddenBranch = Hidden(new Node3D());
        hiddenBranch.AddChild(Mesh(Vector3.One));
        body.AddChild(hiddenBranch);
        var shownBranch = new Node3D();
        shownBranch.AddChild(Mesh(new Vector3(0.1f, 0.1f, 0.1f)));
        body.AddChild(shownBranch);

        var look = StockItemLook.Capture(body)!;

        AssertThat(MeshesIn(look.Node)).IsEqual(2);
        look.Node.Free();
        body.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PrintedLabelsAreCopiedWithTheirTextButAddNothingToTheSize()
    {
        var size = new Vector3(0.3f, 0.2f, 0.25f);
        var body = new Node3D();
        body.AddChild(Mesh(size));
        body.AddChild(new Label3D { Text = "OCTARINE CARDS", FontSize = 52, Position = new Vector3(0f, 0.1f, 0.2f) });
        body.AddChild(Hidden(new Label3D { Text = "HIDDEN" }));

        var look = StockItemLook.Capture(body)!;

        var labels = LabelsIn(look.Node);
        AssertThat(labels.Select(label => label.Text).ToArray()).ContainsExactly("OCTARINE CARDS");
        AssertThat(labels[0].FontSize).IsEqual(52);
        AssertThat(look.Size.IsEqualApprox(size)).IsTrue();
        look.Node.Free();
        body.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnItemShowingOnlyALabelHasNoLook()
    {
        var body = new Node3D();
        body.AddChild(new Label3D { Text = "8 CARDS" });

        AssertThat(StockItemLook.Capture(body)).IsNull();
        body.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AFlatItemIsLaidOnItsWidestFaceWithItsBaseAtTheOrigin()
    {
        var body = new Node3D();
        body.AddChild(Mesh(CardSize));

        var look = StockItemLook.Capture(body)!;

        AssertThat(look.Size.IsEqualApprox(new Vector3(CardSize.X, CardSize.Z, CardSize.Y))).IsTrue();
        var bounds = BoundsOf(look.Node);
        AssertThat(Mathf.Abs(bounds.Position.Y)).IsLess(Tolerance);
        AssertThat(Mathf.Abs(bounds.GetCenter().X)).IsLess(Tolerance);
        AssertThat(Mathf.Abs(bounds.GetCenter().Z)).IsLess(Tolerance);
        look.Node.Free();
        body.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ABoxStaysUpright()
    {
        var size = new Vector3(0.3f, 0.2f, 0.25f);
        var body = new Node3D();
        body.AddChild(Mesh(size));

        var look = StockItemLook.Capture(body)!;

        AssertThat(look.Size.IsEqualApprox(size)).IsTrue();
        look.Node.Free();
        body.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnItemWithNothingVisibleHasNoLook()
    {
        var body = new Node3D();
        body.AddChild(Hidden(Mesh(Vector3.One)));

        AssertThat(StockItemLook.Capture(body)).IsNull();
        body.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DiscardFreesALookNoPropAdopted()
    {
        var body = new Node3D();
        body.AddChild(Mesh(CardSize));
        var look = StockItemLook.Capture(body)!;

        look.Discard();

        AssertThat(GodotObject.IsInstanceValid(look.Node)).IsFalse();
        body.Free();
    }

    private static MeshInstance3D Mesh(Vector3 size) => new() { Mesh = new BoxMesh { Size = size } };

    private static T Hidden<T>(T node) where T : Node3D
    {
        node.Visible = false;
        return node;
    }

    private static System.Collections.Generic.List<Label3D> LabelsIn(Node node) =>
        node.GetChildren().SelectMany(child =>
            child is Label3D label ? [label, .. LabelsIn(child)] : LabelsIn(child)).ToList();

    private static int MeshesIn(Node node) =>
        node.GetChildren().Sum(child => (child is MeshInstance3D ? 1 : 0) + MeshesIn(child));

    // The bounds of every mesh under the look, in the look's own space.
    private static Aabb BoundsOf(Node3D look)
    {
        Aabb? bounds = null;
        Walk(look, Transform3D.Identity);
        return bounds!.Value;

        void Walk(Node node, Transform3D toLook)
        {
            foreach (var child in node.GetChildren().OfType<Node3D>())
            {
                var transform = toLook * child.Transform;
                if (child is MeshInstance3D { Mesh: not null } mesh)
                {
                    var box = transform * mesh.Mesh.GetAabb();
                    bounds = bounds?.Merge(box) ?? box;
                }

                Walk(child, transform);
            }
        }
    }
}
