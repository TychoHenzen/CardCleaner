using Godot;

namespace CardCleaner.Scripts.Features.Shop.People;

/// <summary>
///     Where a <see cref="HumanFigure" /> puts the model it wears: scaled so its meshes stand a given height with their
///     lowest point on the figure's origin, and turned so its face looks down -Z.
/// </summary>
internal static class FigureFit
{
    /// <summary>
    ///     The transform that stands <paramref name="model" /> <paramref name="height" /> metres tall, turned by
    ///     <paramref name="yawDegrees" />. A model with no mesh height is only turned.
    ///     ASSUMPTION (Low): a model stands centred over its own origin, so only its height and floor are corrected.
    /// </summary>
    public static Transform3D Standing(Node3D model, float height, float yawDegrees)
    {
        var turn = new Basis(Vector3.Up, Mathf.DegToRad(yawDegrees));
        var bounds = MeshBounds(model, Transform3D.Identity, null);
        if (bounds is not { } box || box.Size.Y <= 0f)
            return new Transform3D(turn, Vector3.Zero);

        var scale = height / box.Size.Y;
        return new Transform3D(turn.Scaled(Vector3.One * scale), new Vector3(0f, -box.Position.Y * scale, 0f));
    }

    // The box around every mesh under node, in the frame toModel maps node into. The model's own transform is left
    // out: the bounds are measured as if the model stood untransformed on the figure.
    private static Aabb? MeshBounds(Node node, Transform3D toModel, Aabb? bounds)
    {
        if (node is MeshInstance3D { Mesh: not null } mesh)
        {
            var box = toModel * mesh.GetAabb();
            bounds = bounds is { } sofar ? sofar.Merge(box) : box;
        }

        foreach (var child in node.GetChildren())
        {
            var childToModel = child is Node3D spatial ? toModel * spatial.Transform : toModel;
            bounds = MeshBounds(child, childToModel, bounds);
        }

        return bounds;
    }
}
