using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>
///     Still copies of a body's visible meshes and printed labels, in the body's frame, for a
///     <see cref="StockItemLook" />. A hidden node hides everything under it, as it does in the tree.
/// </summary>
internal sealed class VisibleParts
{
    private readonly List<MeshInstance3D> _meshes = new();
    private readonly List<Label3D> _labels = new();

    private IEnumerable<Node3D> All => _meshes.Cast<Node3D>().Concat(_labels);

    /// <summary>Copies the visible meshes and labels under <paramref name="node" />.</summary>
    public void Collect(Node node, Transform3D toBody)
    {
        foreach (var spatial in node.GetChildren().OfType<Node3D>().Where(child => child.Visible))
        {
            var transform = toBody * spatial.Transform;
            switch (spatial)
            {
                case MeshInstance3D { Mesh: not null } mesh:
                    _meshes.Add(Copy(mesh, transform));
                    break;
                case Label3D label:
                    _labels.Add(Copy(label, transform));
                    break;
            }

            Collect(spatial, transform);
        }
    }

    /// <summary>Whether a visible mesh was copied; a body that shows only labels has no look.</summary>
    public bool HasMesh => _meshes.Count > 0;

    /// <summary>
    ///     The box around the copied meshes, of which there must be one (<see cref="HasMesh" />). A label adds nothing
    ///     to it.
    /// </summary>
    public Aabb Bounds() =>
        _meshes.Skip(1).Aggregate(BoxOf(_meshes[0]), (bounds, mesh) => bounds.Merge(BoxOf(mesh)));

    private static Aabb BoxOf(MeshInstance3D mesh) => mesh.Transform * mesh.Mesh.GetAabb();

    /// <summary>
    ///     Puts every copy under a new "Look" root, through a "Laid" node placed at <paramref name="placed" />.
    /// </summary>
    public Node3D Under(Transform3D placed)
    {
        var root = new Node3D { Name = "Look" };
        var holder = new Node3D { Name = "Laid", Transform = placed };
        root.AddChild(holder);
        foreach (var part in All)
            holder.AddChild(part);
        return root;
    }

    /// <summary>Frees the copies, for a body that shows no mesh.</summary>
    public void FreeCopies()
    {
        foreach (var part in All)
            part.Free();
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
