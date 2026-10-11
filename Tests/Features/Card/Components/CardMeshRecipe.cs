using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

/// <summary>
/// The card's authoring CSG tree and its bake, with the numbers CardDesigner.UpdateShape wrote at runtime.
/// Test code only: the shipped mesh is res://Data/Card/CardMesh.tres, and nothing at runtime uses this recipe.
/// </summary>
public static class CardMeshRecipe
{
    public const float DefaultBevelSize = 0.032f;
    public const int DefaultBevelSides = 16;

    private const float InnerThicknessOffset = 0.002f;

    // Same order as the Combiner children in the card scene, so the boolean sees its operands in the same order.
    private static readonly string[] CornerNames =
    {
        "CSGCylinder3D", "CSGCylinder3D2", "CSGCylinder3D3", "CSGCylinder3D4",
    };

    /// <summary>
    /// Builds the tree for a card of the given size. The Combiner intersects the rounded outline of corner cylinders
    /// and trim boxes with the outer box, which is what the scene's operation = 1 (Intersection) does.
    /// </summary>
    public static CsgBox3D BuildCsg(
        float width,
        float height,
        float thickness,
        float bevelSize = DefaultBevelSize,
        int bevelSides = DefaultBevelSides)
    {
        var outer = new CsgBox3D { Name = "OuterBox", Size = new Vector3(width, thickness, height) };
        var combiner = new CsgCombiner3D { Name = "Combiner", Operation = CsgShape3D.OperationEnum.Intersection };
        outer.AddChild(combiner);

        var halfW = width * 0.5f - bevelSize;
        var halfH = height * 0.5f - bevelSize;
        foreach (var name in CornerNames)
        {
            // The name suffix picks the quadrant, as CardDesigner.UpdateShape did.
            var negX = name.EndsWith('2') || name.EndsWith('3');
            var negZ = name.EndsWith('3') || name.EndsWith('4');
            var origin = new Vector3(negX ? -halfW : halfW, 0, negZ ? halfH : -halfH);
            combiner.AddChild(new CsgCylinder3D
            {
                Name = name,
                Radius = bevelSize,
                Height = thickness,
                Sides = bevelSides,
                Transform = new Transform3D(Basis.Identity, origin),
            });
        }

        combiner.AddChild(new CsgBox3D
        {
            Name = "CSGBox3D",
            Size = new Vector3(width, thickness + InnerThicknessOffset, height - 2 * bevelSize),
        });
        combiner.AddChild(new CsgBox3D
        {
            Name = "CSGBox3D2",
            Size = new Vector3(width - 2 * bevelSize, thickness + InnerThicknessOffset, height),
        });
        return outer;
    }

    /// <summary>
    /// Bakes the tree the way CsgBaker did, then remaps the box UVs. Godot builds CSG geometry in a deferred update,
    /// so this returns null until the tree has been updated; callers wait for a non-null result.
    /// </summary>
    public static ArrayMesh? TryBake(CsgShape3D root, float width, float height)
    {
        var baked = root.BakeStaticMesh();
        return baked == null ? null : RemapBoxUVs(baked, width, height);
    }

    // Moved verbatim from CsgBaker.cs; the UV rules are part of the committed mesh.
    private static ArrayMesh RemapBoxUVs(ArrayMesh source, float width, float height)
    {
        var result = new ArrayMesh();
        var surfaces = source.GetSurfaceCount();

        for (var s = 0; s < surfaces; s++)
        {
            var arrays = source.SurfaceGetArrays(s);
            var verts = arrays[(int)Mesh.ArrayType.Vertex].As<Vector3[]>();
            var norms = arrays[(int)Mesh.ArrayType.Normal].As<Vector3[]>();
            var uvs = CalculateUVsForSurface(verts, norms, width, height);

            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            var primType = source.SurfaceGetPrimitiveType(s);
            result.AddSurfaceFromArrays(primType, arrays);
        }

        return result;
    }

    private static Vector2[] CalculateUVsForSurface(Vector3[] vertices, Vector3[] normals, float width, float height)
    {
        var uvs = new Vector2[vertices.Length];

        for (var i = 0; i < vertices.Length; i++) uvs[i] = CalculateVertexUv(vertices[i], normals?[i], width, height);

        return uvs;
    }

    private static Vector2 CalculateVertexUv(Vector3 vertex, Vector3? normal, float width, float height)
    {
        var uBase = vertex.X / width + 0.5f;
        var vBase = vertex.Z / height + 0.5f;
        var v = Mathf.Clamp(vBase, 0, 1);

        var u = normal.HasValue && HasValidNormals(normal.Value)
            ? CalculateUBasedOnNormal(uBase, normal.Value)
            : Mathf.Clamp(uBase, 0, 1);

        return new Vector2(u, v);
    }

    private static bool HasValidNormals(Vector3 normal)
    {
        return normal != Vector3.Zero;
    }

    private static float CalculateUBasedOnNormal(float uBase, Vector3 normal)
    {
        return normal.Y switch
        {
            > 0.9f => Mathf.Clamp(uBase * 0.5f, 0f, 0.5f),
            < -0.9f => Mathf.Clamp(uBase * 0.5f + 0.5f, 0.5f, 1f),
            _ => Mathf.Clamp(uBase, 0, 1)
        };
    }
}
