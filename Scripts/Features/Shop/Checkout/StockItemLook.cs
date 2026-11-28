using Godot;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

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
        var parts = new VisibleParts();
        parts.Collect(body, Transform3D.Identity);
        if (!parts.HasMesh)
        {
            parts.FreeCopies();
            return null;
        }

        var bounds = parts.Bounds();
        var lay = new Transform3D(LayFlat(bounds.Size), Vector3.Zero);
        var laid = lay * bounds;
        var centre = laid.GetCenter();
        var placed = new Transform3D(Basis.Identity, new Vector3(-centre.X, -laid.Position.Y, -centre.Z)) * lay;
        return new StockItemLook(parts.Under(placed), laid.Size);
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
}
