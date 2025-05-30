using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Utilities;

[Tool]
public partial class CsgBaker : CsgBox3D, ICardComponent
{
    private static readonly Dictionary<string, ArrayMesh> _meshCache = new();


    private bool _baked;
    [Export] public bool BakeOnSetup = true;
    [Export] public bool DebugUVs;

    public void Setup(RigidBody3D cardRoot)
    {
        if (_baked || !BakeOnSetup) return;
        CallDeferred(nameof(PerformDeferredBake), cardRoot);
    }

    private void PerformDeferredBake(RigidBody3D cardRoot)
    {
        if (_baked) return;
        BakeToMesh(cardRoot);
        _baked = true;
    }

    private void BakeToMesh(RigidBody3D cardRoot)
    {
        var designer = cardRoot.GetNode<Features.Card.Components.CardDesigner>("Designer");
        var cacheKey = $"{designer.Width}x{designer.Height}x{designer.Thickness}";

        if (!_meshCache.TryGetValue(cacheKey, out var cachedMesh))
        {
            // Only bake if not cached
            CsgShape3D rootCsg = this;
            while (rootCsg.GetParent() is CsgShape3D parent) rootCsg = parent;

            var bakedMesh = rootCsg.BakeStaticMesh();
            cachedMesh = RemapBoxUVs(bakedMesh, designer.Width, designer.Height);
            _meshCache[cacheKey] = cachedMesh;
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


    private ArrayMesh RemapBoxUVs(ArrayMesh source, float width, float height)
    {
        var result = new ArrayMesh();
        var surfaces = source.GetSurfaceCount();

        for (var s = 0; s < surfaces; s++)
        {
            var arrays = source.SurfaceGetArrays(s);
            var verts = arrays[(int)Mesh.ArrayType.Vertex].As<Vector3[]>();
            var norms = arrays[(int)Mesh.ArrayType.Normal].As<Vector3[]>();

            var uvs = new Vector2[verts.Length];

            for (var i = 0; i < verts.Length; i++)
            {
                // Normalize planar coords: X → U, Z → V
                var uBase = verts[i].X / width + 0.5f;
                var vBase = verts[i].Z / height + 0.5f;
                float u, v = Mathf.Clamp(vBase, 0, 1);

                if (norms != null && norms.Length == verts.Length)
                {
                    var ny = norms[i].Y;
                    if (ny > 0.9f)
                    {
                        // Front face → left half [0, 0.5]
                        u = uBase * 0.5f;
                        u = Mathf.Clamp(u, 0f, 0.5f);
                    }
                    else if (ny < -0.9f)
                    {
                        // Back face → right half [0.5, 1]
                        u = uBase * 0.5f + 0.5f;
                        u = Mathf.Clamp(u, 0.5f, 1f);
                    }
                    else
                    {
                        // Side faces → full width (optional)
                        u = Mathf.Clamp(uBase, 0, 1);
                    }
                }
                else
                {
                    u = Mathf.Clamp(uBase, 0, 1);
                }

                uvs[i] = new Vector2(u, v);

                if (DebugUVs)
                    GD.Print(
                        $"[CsgBaker] Surface {s}, Vertex {i}: Pos={verts[i]}, Norm={(norms != null ? norms[i] : Vector3.Zero)}, UV={uvs[i]}");
            }

            arrays[(int)Mesh.ArrayType.TexUV] = uvs;

            var primType = source.SurfaceGetPrimitiveType(s);
            result.AddSurfaceFromArrays(primType, arrays);
        }

        return result;
    }
}