using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A still copy of a stock item's visible meshes and printed labels, made when a customer hands the real body over
///     at the checkout, so the counter can show the item that is being sold. The copy has no script, collision or
///     physics. Its origin is the centre of its base, and a flat item (a card, a booster) is laid on its widest face.
///     Only the meshes decide its size: a label is printed on a face and adds nothing to it.
/// </summary>
public sealed class StockItemLook
{
    // An item whose thinnest side is under this share of its middle side counts as flat and is laid down.
    private const float FlatRatio = 0.34f;

    private StockItemLook(Node3D node, Vector3 size)
    {
        Node = node;
        Size = size;
    }

    /// <summary>The copied meshes and labels under one parent; not in the tree until a prop adopts it.</summary>
    public Node3D Node { get; }

    /// <summary>The size of the laid-down copy's bounding box.</summary>
    public Vector3 Size { get; }

    /// <summary>Copies the visible meshes and labels of <paramref name="body" />; null when it shows no mesh.</summary>
    public static StockItemLook? Capture(Node3D body)
    {
        var meshes = new List<MeshInstance3D>();
        var labels = new List<Label3D>();
        CollectVisible(body, Transform3D.Identity, meshes, labels);
        if (meshes.Count == 0)
        {
            foreach (var label in labels)
                label.Free();
            return null;
        }

        var bounds = meshes[0].Transform * meshes[0].Mesh.GetAabb();
        foreach (var mesh in meshes)
            bounds = bounds.Merge(mesh.Transform * mesh.Mesh.GetAabb());

        var lay = new Transform3D(LayFlat(bounds.Size), Vector3.Zero);
        var laid = lay * bounds;
        var centre = laid.GetCenter();
        var placed = new Transform3D(Basis.Identity, new Vector3(-centre.X, -laid.Position.Y, -centre.Z)) * lay;

        var root = new Node3D { Name = "Look" };
        var holder = new Node3D { Name = "Laid", Transform = placed };
        root.AddChild(holder);
        foreach (var mesh in meshes)
            holder.AddChild(mesh);
        foreach (var label in labels)
            holder.AddChild(label);

        return new StockItemLook(root, laid.Size);
    }

    /// <summary>Frees the copy when no prop adopted it.</summary>
    public void Discard()
    {
        if (GodotObject.IsInstanceValid(Node) && !Node.IsInsideTree() && Node.GetParent() == null)
            Node.Free();
    }

    // The rotation that turns the thinnest side up when the item is flat, else none.
    private static Basis LayFlat(Vector3 size)
    {
        var sides = new[] { size.X, size.Y, size.Z };
        var thinnest = 0;
        for (var axis = 1; axis < sides.Length; axis++)
        {
            if (sides[axis] < sides[thinnest])
                thinnest = axis;
        }

        System.Array.Sort(sides);
        if (thinnest == 1 || sides[0] >= sides[1] * FlatRatio)
            return Basis.Identity;

        return thinnest == 0
            ? new Basis(Vector3.Back, Mathf.Pi / 2f)
            : new Basis(Vector3.Right, -Mathf.Pi / 2f);
    }

    // Walks the visible subtree; a hidden node hides everything under it, as it does in the tree.
    private static void CollectVisible(Node node, Transform3D toBody, List<MeshInstance3D> meshes, List<Label3D> labels)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is not Node3D spatial)
                continue;
            if (!spatial.Visible)
                continue;

            var transform = toBody * spatial.Transform;
            if (spatial is MeshInstance3D { Mesh: not null } mesh)
                meshes.Add(Copy(mesh, transform));
            else if (spatial is Label3D label)
                labels.Add(Copy(label, transform));
            CollectVisible(spatial, transform, meshes, labels);
        }
    }

    private static MeshInstance3D Copy(MeshInstance3D source, Transform3D transform)
    {
        var copy = new MeshInstance3D
        {
            Mesh = source.Mesh,
            MaterialOverride = source.MaterialOverride,
            MaterialOverlay = source.MaterialOverlay,
            CastShadow = source.CastShadow,
            Transform = transform
        };
        for (var surface = 0; surface < source.GetSurfaceOverrideMaterialCount(); surface++)
            copy.SetSurfaceOverrideMaterial(surface, source.GetSurfaceOverrideMaterial(surface));
        return copy;
    }

    // A label copies every property (text, font, size, colour, shading); its children are walked on their own.
    private static Label3D Copy(Label3D source, Transform3D transform)
    {
        var copy = (Label3D)source.Duplicate(0);
        foreach (var child in copy.GetChildren())
        {
            copy.RemoveChild(child);
            child.Free();
        }

        copy.Transform = transform;
        return copy;
    }
}
