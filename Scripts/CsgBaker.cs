using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts
{
    [Tool]
    public partial class CsgBaker : CsgBox3D, ICardComponent
    {
        [Export] public bool BakeOnSetup = true;
        [Export] public bool DebugUVs = false;

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
            CsgShape3D rootCsg = this;
            while (rootCsg.GetParent() is CsgShape3D parent) rootCsg = parent;

            var bakedMesh = rootCsg.BakeStaticMesh();
            if (bakedMesh == null)
            {
                GD.PrintErr($"CSGBaker: BakeStaticMesh returned null on '{rootCsg.Name}'");
                return;
            }

            var designer = cardRoot.GetNode<CardDesigner>("Designer");
            float width  = designer.Width;
            float height = designer.Height;

            var correctedMesh = RemapBoxUVs(bakedMesh, width, height);

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
                var arrays  = source.SurfaceGetArrays(s);
                var verts   = arrays[(int)Mesh.ArrayType.Vertex].As<Vector3[]>();
                var norms   = arrays[(int)Mesh.ArrayType.Normal].As<Vector3[]>();
                
                var uvs = new Vector2[verts.Length];

                for (int i = 0; i < verts.Length; i++)
                {
                    // Normalize planar coords: X → U, Z → V
                    float uBase = verts[i].X / width  + 0.5f;
                    float vBase = verts[i].Z / height + 0.5f;
                    float u, v = Mathf.Clamp(vBase, 0, 1);

                    if (norms != null && norms.Length == verts.Length)
                    {
                        float ny = norms[i].Y;
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
                        GD.Print($"[CsgBaker] Surface {s}, Vertex {i}: Pos={verts[i]}, Norm={(norms != null ? norms[i] : Vector3.Zero)}, UV={uvs[i]}");
                }

                arrays[(int)Mesh.ArrayType.TexUV] = uvs;

                var primType = source.SurfaceGetPrimitiveType(s);
                result.AddSurfaceFromArrays(primType, arrays);
            }

            return result;
        }

        public void IntegrateForces(PhysicsDirectBodyState3D state) { /* no-op */ }
    }
}
