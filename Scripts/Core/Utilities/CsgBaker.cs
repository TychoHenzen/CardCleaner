using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using Godot;

namespace CardCleaner.Scripts.Core.Utilities;

[Tool]
public partial class CsgBaker : CsgBox3D, ICardComponent
{
    private static readonly Dictionary<string, ArrayMesh> MeshCache = new();
    private bool _baked;
    [Export] public bool BakeOnSetup = true;
    [Export] public bool DebugUVs;
    [Export] public CardDesigner? Designer { get; set; }

    public void Setup(Node cardRoot)
    {
        if (_baked || !BakeOnSetup) return;
        if (Designer == null)
        {
            Designer = cardRoot.GetNodeOrNull<CardDesigner>("Designer");
        }
        CallDeferred(MethodName.PerformDeferredBake, cardRoot);
    }

    private void PerformDeferredBake(Node? cardRoot)
    {
        if (_baked || cardRoot == null) return;
        BakeToMesh(cardRoot);
        _baked = true;
    }

    private void BakeToMesh(Node cardRoot)
    {
        if (Designer == null)
            return;
        var cacheKey = $"{Designer.Width}x{Designer.Height}x{Designer.Thickness}";

        if (!MeshCache.TryGetValue(cacheKey, out var cachedMesh))
        {
            // Only bake if not cached
            CsgShape3D rootCsg = this;
            while (rootCsg.GetParent() is CsgShape3D parent) rootCsg = parent;

            var bakedMesh = rootCsg.BakeStaticMesh();
            cachedMesh = RemapBoxUVs(bakedMesh, Designer.Width, Designer.Height);
            MeshCache[cacheKey] = cachedMesh;
        }

        var meshInstance = new MeshInstance3D
        {
            Name = $"{Name}_Baked",
            Mesh = cachedMesh,
            MaterialOverride = MaterialOverride
        };
        cardRoot.AddChild(meshInstance);
        Visible = false;
    }


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

        for (var i = 0; i < vertices.Length; i++)
        {
            uvs[i] = CalculateVertexUv(vertices[i], normals?[i], width, height);
        }

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

    private static bool HasValidNormals(Vector3 normal) => normal != Vector3.Zero;

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