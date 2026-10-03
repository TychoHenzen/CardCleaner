namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshGeneration;
using Godot;
using System;
using System.Linq;

/// <summary>Generates an irregular quad mesh from a hexagonal base grid.</summary>
public static class MeshGenerator
{
    /// <summary>
    /// Configuration for mesh generation.
    /// </summary>
    public class GenerationConfig
    {
        /// <summary>Number of hexagonal rings from center (determines mesh size).</summary>
        public int Rings { get; set; } = 10;

        /// <summary>Radius of each hexagon in world units.</summary>
        public float HexRadius { get; set; } = 1.0f;

        /// <summary>Probability of merging two adjacent triangles into a quad.</summary>
        public float MergeProbability { get; set; } = 0.7f;

        /// <summary>Number of Lloyd relaxation iterations.</summary>
        public int RelaxationIterations { get; set; } = 15;

        /// <summary>Whether to pin boundary vertices during relaxation.</summary>
        public bool PinBoundary { get; set; } = true;

        /// <summary>Random seed for reproducible generation (null = random).</summary>
        public int? Seed { get; set; }
    }

    /// <summary>
    /// Generate an irregular quad mesh with default configuration.
    /// </summary>
    public static IrregularMesh Generate(int rings = 10, int? seed = null)
    {
        return Generate(new GenerationConfig { Rings = rings, Seed = seed });
    }

    /// <summary>
    /// Generate an irregular quad mesh with custom configuration.
    /// </summary>
    public static IrregularMesh Generate(GenerationConfig config)
    {
        var random = config.Seed.HasValue
            ? new Random(config.Seed.Value)
            : new Random();

        var workingMesh = HexGridGenerator.Generate(config.Rings, config.HexRadius);
        GD.Print($"Step 1: Generated {workingMesh.Vertices.Count} vertices, {workingMesh.Faces.Count} triangles");

        TriangleMerger.Merge(workingMesh, config.MergeProbability, random);
        int numTris = workingMesh.Faces.Count(f => f.IsTriangle);
        int numQuads = workingMesh.Faces.Count(f => f.IsQuad);
        GD.Print($"Step 2: After merging - {numQuads} quads, {numTris} remaining triangles");

        MeshSubdivider.SubdivideAllFaces(workingMesh);
        numQuads = workingMesh.Faces.Count(f => f.IsQuad);
        GD.Print($"Step 3: After subdivision - {numQuads} quads, {workingMesh.Vertices.Count} vertices");

        if (config.RelaxationIterations > 0)
        {
            MeshRelaxer.Relax(workingMesh, config.RelaxationIterations, config.PinBoundary);
        }

        var finalMesh = MeshConverter.Convert(workingMesh);
        GD.Print($"Final mesh: {finalMesh.GetStatistics()}");

        return finalMesh;
    }
}
