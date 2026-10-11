using Godot;

namespace CardCleaner.Tests.Features.Card.Components;

/// <summary>
/// The source of the card's shape: a CSG tree and its bake, which produce res://Data/Card/CardMesh.tres.
/// The sizes are computed from width, height and bevel, not copied from the old scene text, because the runtime
/// CSG the mesh replaced computed them the same way (the scene's stored numbers differ by a float32 ulp).
/// Test code only: nothing at runtime uses this recipe. CardMeshBakeTest regenerates the mesh from it.
/// </summary>
public static class CardMeshRecipe
{
    public const float DefaultBevelSize = 0.032f;
    public const int DefaultBevelSides = 16;

    private const float InnerThicknessOffset = 0.002f;

    // Order of the Combiner children the committed mesh was baked from, so the boolean sees the same operands.
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
            // The name suffix picks the quadrant: 2 and 3 sit at negative X, 3 and 4 at positive Z.
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
    /// Bakes the tree into a static mesh, then remaps the box UVs. Godot builds CSG geometry in a deferred update,
    /// so this returns null until the tree has been updated; callers wait for a non-null result.
    /// </summary>
    public static ArrayMesh? TryBake(CsgShape3D root, float width, float height)
    {
        var baked = root.BakeStaticMesh();
        return baked == null ? null : RemapBoxUVs(baked, width, height);
    }

    // The UV layout the card material expects: the +Y face maps to the left half of the texture, the -Y face to the
    // right half, and the edges to the full width. The committed mesh stores these UVs, so changing them changes the
    // mesh and needs a regeneration.
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
