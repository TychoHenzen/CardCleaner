using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug.Painting;

/// <summary>
/// Default values of the IrregularMeshPreview inspector properties, used for the reset-to-default button.
/// </summary>
internal static class PreviewDefaults
{
    internal const int Rings = 6;
    internal const float HexRadius = 1.0f;
    internal const float MergeProbability = 0.7f;
    internal const int RelaxationIterations = 15;
    internal const int Seed = 12345;
    internal const int NumIslands = 5;
    internal const float VisualScale = 50f;

    /// <summary>
    /// Returns the default for the named property, or null when the property has no revert default.
    /// </summary>
    internal static Variant? Find(string property)
    {
        return property switch
        {
            nameof(IrregularMeshPreview.Rings) => Rings,
            nameof(IrregularMeshPreview.HexRadius) => HexRadius,
            nameof(IrregularMeshPreview.MergeProbability) => MergeProbability,
            nameof(IrregularMeshPreview.RelaxationIterations) => RelaxationIterations,
            nameof(IrregularMeshPreview.Seed) => Seed,
            nameof(IrregularMeshPreview.NumIslands) => NumIslands,
            nameof(IrregularMeshPreview.VisualScale) => VisualScale,
            _ => null
        };
    }
}
