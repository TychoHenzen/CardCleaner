using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts;

[Tool]
public partial class CsgBaker : CsgBox3D, ICardComponent
{
    [Export] public bool BakeOnSetup = true;
    private bool _baked;

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
        // 1) Bake CSG to a mesh
        CsgShape3D rootCsg = this;
        while (rootCsg.GetParent() is CsgShape3D parent) rootCsg = parent;

        var bakedMesh = rootCsg.BakeStaticMesh() as ArrayMesh;
        if (bakedMesh == null)
        {
            GD.PrintErr($"CSGBaker: BakeStaticMesh returned null on '{rootCsg.Name}'");
            return;
        }

        // 2) Fetch card dimensions from CardDesigner
        var designer = cardRoot.GetNode<CardDesigner>("Designer");
        float width  = designer.Width;
        float height = designer.Height;

        // 3) Remap UVs via box‐projection
        var correctedMesh = RemapBoxUVs(bakedMesh, width, height);

        // 4) Instance the corrected mesh
        var meshInstance = new MeshInstance3D
        {
            Name = $"{rootCsg.Name}_Baked",
            Mesh = correctedMesh,
            MaterialOverride = MaterialOverride
        };
        cardRoot.AddChild(meshInstance);
        rootCsg.Visible = false;
    }

    private ArrayMesh RemapBoxUVs(ArrayMesh source, float width, float height)
    {
        var result = new ArrayMesh();
        int surfaces = source.GetSurfaceCount();

        for (int s = 0; s < surfaces; s++)
        {
            // 1) Pull out the vertex, normal, and existing arrays
            var arrays  = source.SurfaceGetArrays(s);
            var verts = arrays[(int)Mesh.ArrayType.Vertex].As<Vector3[]>();
            var norms = arrays[(int)Mesh.ArrayType.Normal].As<Vector3[]>();

            // 2) Prepare a new UV array
            var uvs = new Vector2[verts.Length];

            for (int i = 0; i < verts.Length; i++)
            {
                // base planar mapping onto XZ plane
                float uBase = verts[i].X / width  + 0.5f;
                float vBase = verts[i].Z / height + 0.5f;
                float u, v = vBase;

                // if normals are available, split top vs bottom
                if (norms != null && norms.Length == verts.Length)
                {
                    float ny = norms[i].Y;
                    if (ny > 0.9f)
                    {
                        // Top face → left half of atlas
                        u = uBase * 0.5f;
                    }
                    else if (ny < -0.9f)
                    {
                        // Bottom face → right half of atlas
                        u = uBase * 0.5f + 0.5f;
                    }
                    else
                    {
                        // Side faces → full range (or tweak as needed)
                        u = uBase;
                    }
                }
                else
                {
                    // No normals? Fall back to full mapping
                    u = uBase;
                }

                uvs[i] = new Vector2(u, v);
            }

            // 3) Replace the TexUv channel
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;

            
            // 4) Add the fixed-UV surface to the new mesh
            var primType = source.SurfaceGetPrimitiveType(s);

            result.AddSurfaceFromArrays(primType, arrays);
        }

        return result;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state) { /* no-op */ }
}