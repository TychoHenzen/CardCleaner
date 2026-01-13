using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// Sets up a 3D mesh and material to display a SubViewport texture in world space.
/// Reusable component for both SimpleWorldMapScreen and IrregularWorldMapScreen.
/// </summary>
public static class ViewportScreenSetup
{
    /// <summary>
    /// Create a custom mesh with explicit UV coordinates for proper texture mapping.
    /// </summary>
    /// <param name="screenMesh">The MeshInstance3D to set up.</param>
    /// <param name="width">Half-width of the quad.</param>
    /// <param name="height">Half-height of the quad.</param>
    public static void SetupScreenMesh(MeshInstance3D screenMesh, float width = 2.6665f, float height = 1.5f)
    {
        // Create a custom mesh with explicit UV coordinates
        var arrayMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        // Define the quad vertices (matching the desired screen size)
        var vertices = new Vector3[]
        {
            new(-width, -height, 0), // Bottom-left
            new(width, -height, 0),  // Bottom-right
            new(width, height, 0),   // Top-right
            new(-width, height, 0)   // Top-left
        };

        // Critical: UV coordinates that properly map the texture
        var uvs = new Vector2[]
        {
            new(0, 1), // Bottom-left maps to (0,1) - bottom of texture
            new(1, 1), // Bottom-right maps to (1,1) - bottom-right of texture
            new(1, 0), // Top-right maps to (1,0) - top-right of texture
            new(0, 0)  // Top-left maps to (0,0) - top-left of texture
        };

        // Triangle indices for two triangles making a quad
        var indices = new int[]
        {
            0, 1, 2, // First triangle
            0, 2, 3  // Second triangle
        };

        // Normals pointing toward camera
        var normals = new[] { Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward };

        // Assign arrays
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        // Create the mesh surface
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        // Assign the custom mesh
        screenMesh.Mesh = arrayMesh;

        ILog.Print("[ViewportScreenSetup] Custom screen mesh with proper UVs created");
    }

    /// <summary>
    /// Create a material that displays the viewport texture on the mesh.
    /// </summary>
    /// <param name="viewport">The SubViewport to display.</param>
    /// <param name="screenMesh">The MeshInstance3D to apply the material to.</param>
    public static void SetupScreenMaterial(SubViewport viewport, MeshInstance3D screenMesh)
    {
        // Create a completely new material to avoid any conflicts
        var material = new StandardMaterial3D();

        // Set up the material properties for proper viewport display
        material.AlbedoTexture = viewport.GetTexture();
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.DisableReceiveShadows = true;
        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Show both sides

        // Critical: Ensure proper UV mapping
        material.Uv1Scale = Vector3.One;
        material.Uv1Offset = -Vector3.One;

        // Force the material as an override
        screenMesh.MaterialOverride = material;

        ILog.Print("[ViewportScreenSetup] Screen material setup complete");
    }
}
